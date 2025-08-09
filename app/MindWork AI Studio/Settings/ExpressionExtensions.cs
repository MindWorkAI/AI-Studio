using System.Linq.Expressions;

namespace AIStudio.Settings;

public static class ExpressionExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(typeof(ExpressionExtensions));

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