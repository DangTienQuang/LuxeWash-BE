using BLL.Services.AI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Helpers
{
    public class ReflectionHelper : IReflectionHelper
    {
        public object? GetPropertyValue(object target, string propertyName)
        {
            if (target == null)
                return null;

            var property = target
                .GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            return property?.GetValue(target);
        }

        public Type? GetPropertyType(object target, string propertyName)
        {
            if (target == null)
                return null;

            var property = target
                .GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            return property?.PropertyType;
        }

        public bool PropertyExists(object target, string propertyName)
        {
            if (target == null)
                return false;

            return target
                .GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase) != null;
        }
    }
}
