using System.Linq.Expressions;

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
}