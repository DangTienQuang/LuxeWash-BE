using BLL.Services.AI.Interfaces;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Calculators
{
    public class ConditionEvaluator : IConditionEvaluator
    {
        private readonly IReflectionHelper _reflection;
        public ConditionEvaluator(IReflectionHelper reflection)
        {
            _reflection = reflection;
        }

        public bool Evaluate(CustomerFeatureProfile profile, ScenarioCondition condition)
        {
            var propertyName = condition.Feature.PropertyName;

            var actualValue =
                _reflection.GetPropertyValue(
                    profile,
                    propertyName);

            return Compare(
                actualValue,
                condition.Operator,
                condition.ComparisonValue);
        }

        public bool Evaluate(CustomerFeatureProfile profile, ScenarioExclusion exclusion)
        {
            var propertyName = exclusion.Feature.PropertyName;

            var actualValue = _reflection.GetPropertyValue(profile, propertyName);

            return Compare(actualValue, exclusion.Operator, exclusion.ComparisonValue);
        }

        private bool Compare(object actual, string op, string expected)
        {
            if (actual == null)
                return op == "!="; // or whatever null-handling makes sense for your domain

            switch (op)
            {
                case "==":
                case "Equals":
                    return CompareValues(actual, expected) == 0;
                case "!=":
                case "NotEquals":
                    return CompareValues(actual, expected) != 0;
                case ">":
                case "GreaterThan":
                    return CompareValues(actual, expected) > 0;
                case ">=":
                case "GreaterThanOrEqual":
                    return CompareValues(actual, expected) >= 0;
                case "<":
                case "LessThan":
                    return CompareValues(actual, expected) < 0;
                case "<=":
                case "LessThanOrEqual":
                    return CompareValues(actual, expected) <= 0;
                case "Contains":
                    return actual.ToString().Contains(expected, StringComparison.OrdinalIgnoreCase);
                default:
                    throw new NotSupportedException($"Unsupported operator: {op}");
            }
        }

        // Rename your original method — it returns an int (comparison result), not a bool,
        // so keep it separate and give it a distinct name to avoid overload ambiguity.
        private int CompareValues(object actual, string expected)
        {
            if (actual is int i)
                return i.CompareTo(int.Parse(expected));
            if (actual is long l)
                return l.CompareTo(long.Parse(expected));
            if (actual is decimal dec)
                return dec.CompareTo(decimal.Parse(expected));
            if (actual is double dbl)
                return dbl.CompareTo(double.Parse(expected));
            if (actual is float fl)
                return fl.CompareTo(float.Parse(expected));
            if (actual is DateTime dt)
                return dt.CompareTo(DateTime.Parse(expected));
            return string.Compare(actual.ToString(), expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
