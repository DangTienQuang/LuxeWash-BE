using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface IReflectionHelper
    {
        object? GetPropertyValue(object target, string propertyName);
        Type? GetPropertyType(object target, string propertyName);
        bool PropertyExists(object target, string propertyName);
    }
}
