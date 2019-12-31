using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Violet.WorkItems.Types;
using Violet.WorkItems.ValueProvider;

namespace Violet.WorkItems.Validation
{
    public class ValidationManager
    {
        private readonly DescriptorManager _descriptorManager;

        public ValidationManager(DescriptorManager descriptorManager)
        {
            _descriptorManager = descriptorManager;
        }

        public async Task<IEnumerable<ErrorMessage>> ValidateAsync(WorkItem workItem, IEnumerable<PropertyChange> appliedChanges)
        {
            var errors = new List<ErrorMessage>();
            var validators = CreateValidators(workItem, appliedChanges, errors);

            foreach (var validator in validators)
            {
                var errorsOfProperty = await validator.ValidateAsync(workItem, appliedChanges);

                errors.AddRange(errorsOfProperty);
            }

            return errors;
        }

        private IEnumerable<IValidator> CreateValidators(WorkItem workItem, IEnumerable<PropertyChange> appliedChanges, List<ErrorMessage> errors)
        {
            var validators = new List<IValidator>();

            validators.AddRange(CreateWorkItemValidators(workItem));

            var propertyDescriptors = _descriptorManager.GetCurrentPropertyDescriptors(workItem);
            // validate all properties, even if no change applied. context may change.
            foreach (var pd in propertyDescriptors)
            {
                validators.AddRange(CreatePropertyValidatorByPropertyDescriptor(pd));

                validators.AddRange(pd.Validators.Select(vd => CreatePropertyValidatorByValidatorDescriptor(pd, vd)));

                validators.Add(CreateValueProviderValidator(pd, pd.ValueProvider));
            }

            return validators.Where(v => v != null);
        }

        private IEnumerable<IValidator> CreateWorkItemValidators(WorkItem workItem)
        {
            yield return new CompletenessValidator(_descriptorManager.GetCurrentPropertyDescriptors(workItem));
        }

        private IEnumerable<IValidator> CreatePropertyValidatorByPropertyDescriptor(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.IsEditable == false)
            {
                yield return new ImmutableValidator(propertyDescriptor);
            }
            if (propertyDescriptor.PropertyType == PropertyType.Raw)
            {
                yield return new RawDataTypeValidator(propertyDescriptor.Name);
            }
        }

        private IValidator CreatePropertyValidatorByValidatorDescriptor(PropertyDescriptor propertyDescriptor, ValidatorDescriptor validatorDescriptor)
            => validatorDescriptor switch
            {
                MandatoryValidatorDescriptor mvd => new MandatoryValidator(propertyDescriptor, mvd),
                StringLengthValidatorDescriptor slvd => new StringLengthValidator(propertyDescriptor, slvd),
                _ => null,
            };

        private IValidator CreateValueProviderValidator(PropertyDescriptor propertyDescriptor, ValueProviderDescriptor valueProviderDescriptor)
            => valueProviderDescriptor switch
            {
                EnumValueProviderDescriptor evpd => new ValueProviderValidator(propertyDescriptor, new EnumValueProvider(evpd)),
                ProjectCollectionValueProviderDescriptor pcvpd => null,
                ProjectUserValueProviderDescriptor puvpd => null,
                RelationshipValueProviderDescriptor rvpd => null,
                _ => null,
            };
    }
}