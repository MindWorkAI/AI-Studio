using System.Linq.Expressions;

using AIStudio.Settings.DataModel;

namespace AIStudio.Settings;

public sealed record NoConfig<TClass, TValue> : ConfigMeta<TClass, TValue>
{
    public NoConfig(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, TValue>> propertyExpression) : base(configSelection, propertyExpression)
    {
    }
}