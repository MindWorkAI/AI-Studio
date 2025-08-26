using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace AIStudio.Tools;

public static class ExpressionExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(typeof(ExpressionExtensions));

    /// <summary>
    /// Extracts the member expression from a given lambda expression representing a property.
    /// </summary>
    /// <param name="expression">A lambda expression specifying the property for which the member expression is to be extracted.
    /// The lambda expression body must represent member access.</param>
    /// <typeparam name="TIn">The type of the object containing the property referenced in the lambda expression.</typeparam>
    /// <typeparam name="TOut">The type of the property being accessed in the lambda expression.</typeparam>
    /// <returns>The member expression that represents the property access.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided lambda expression does not represent a valid property expression.</exception>
    public static MemberExpression GetMemberExpression<TIn, TOut>(this Expression<Func<TIn, TOut>> expression)
    {
        switch (expression.Body)
        {
            // Case for value types, which are wrapped in UnaryExpression:
            case UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression:
                return (MemberExpression)unaryExpression.Operand;

            // Case for reference types, which are directly MemberExpressions:
            case MemberExpression memberExpression:
                return memberExpression;
            
            default:
                LOGGER.LogError($"Expression '{expression}' is not a valid property expression.");
                throw new ArgumentException($"Expression '{expression}' is not a valid property expression.", nameof(expression));
        }
    }

    /// <summary>
    /// Attempts to increment the value of an uint property for a specified object using a
    /// provided expression.
    /// </summary>
    /// <param name="expression">An expression representing the property to be incremented. The property
    /// must be of type uint and belong to the provided object.</param>
    /// <param name="data">The object that contains the property referenced by the expression.</param>
    /// <param name="type">The type of increment operation to perform (e.g., prefix or postfix).</param>
    /// <typeparam name="TIn">The type of the object that contains the property to be incremented.</typeparam>
    /// <typeparam name="TOut">The type of the property to be incremented.</typeparam>
    /// <returns>An IncrementResult object containing the result of the increment operation.</returns>
    public static IncrementResult<TOut> TryIncrement<TIn, TOut>(this Expression<Func<TIn, TOut>> expression, TIn data, IncrementType type) where TOut : IBinaryInteger<TOut>
    {
        // Ensure that the expression body is a member expression:
        if (expression.Body is not MemberExpression memberExpression)
            return new(false, TOut.Zero);

        // Ensure that the member expression is a property:
        if (memberExpression.Member is not PropertyInfo propertyInfo)
            return new(false, TOut.Zero);
        
        // Ensure that the member expression has a target object:
        if (memberExpression.Expression is null)
            return new(false, TOut.Zero);
	
        // Get the target object for the expression, which is the object containing the property to increment:
        var targetObjectExpression = Expression.Lambda(memberExpression.Expression, expression.Parameters);
        
        // Compile the lambda expression to get the target object
        // (which is the object containing the property to increment):
        var targetObject = targetObjectExpression.Compile().DynamicInvoke(data);

        // Was the compilation successful?
        if (targetObject is null)
            return new(false, TOut.Zero);

        // Read the current value of the property:
        if (propertyInfo.GetValue(targetObject) is not TOut value)
            return new(false, TOut.Zero);

        // Increment the value:
        switch (type)
        {
            case IncrementType.PRE:
                var nextValue = value + TOut.CreateChecked(1);
                propertyInfo.SetValue(targetObject, nextValue);
                return new(true, nextValue);
            
            case IncrementType.POST:
                var currentValue = value;
                var incrementedValue = value + TOut.CreateChecked(1);
                propertyInfo.SetValue(targetObject, incrementedValue);
                return new(true, currentValue);
            
            default:
                return new(false, TOut.Zero);
        }
    }
}