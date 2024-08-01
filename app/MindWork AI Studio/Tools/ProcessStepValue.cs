using System.Globalization;
using System.Numerics;

namespace AIStudio.Tools;

public readonly record struct ProcessStepValue(int Step, string Name) : INumber<ProcessStepValue>
{
    public static implicit operator int(ProcessStepValue process) => process.Step;
    
    #region INumber implementation
    
    #region Implementation of IComparable
    
    public int CompareTo(object? obj) => this.Step.CompareTo(obj);
    
    #endregion
    
    #region Implementation of IComparable<in ProcessStepValue>
    
    public int CompareTo(ProcessStepValue other) => this.Step.CompareTo(other.Step);
    
    #endregion
    
    #region Implementation of IFormattable
    
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Step.ToString(format, formatProvider);
    
    #endregion
    
    #region Implementation of IParsable<ProcessStepValue>
    
    public static ProcessStepValue Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider), string.Empty);

    public static bool TryParse(string? s, IFormatProvider? provider, out ProcessStepValue result)
    {
        if (int.TryParse(s, provider, out var stepValue))
        {
            result = new ProcessStepValue(stepValue, string.Empty);
            return true;
        }
        
        result = default;
        return false;
    }
    #endregion
    
    #region Implementation of ISpanFormattable
    
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Step.TryFormat(destination, out charsWritten, format, provider);

    #endregion
    
    #region Implementation of ISpanParsable<ProcessStepValue>
    
    public static ProcessStepValue Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(int.Parse(s, provider), string.Empty);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ProcessStepValue result)
    {
        if (int.TryParse(s, provider, out int stepValue))
        {
            result = new ProcessStepValue(stepValue, string.Empty);
            return true;
        }
        
        result = default;
        return false;
    }
    
    #endregion
    
    #region Implementation of IAdditionOperators<ProcessStepValue,ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator +(ProcessStepValue left, ProcessStepValue right) => left with { Step = left.Step + right.Step };

    #endregion
    
    #region Implementation of IAdditiveIdentity<ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue AdditiveIdentity => new(0, string.Empty);
    
    #endregion
    
    #region Implementation of IComparisonOperators<ProcessStepValue,ProcessStepValue,bool>
    
    public static bool operator >(ProcessStepValue left, ProcessStepValue right) => left.Step > right.Step;

    public static bool operator >=(ProcessStepValue left, ProcessStepValue right) => left.Step >= right.Step;

    public static bool operator <(ProcessStepValue left, ProcessStepValue right) => left.Step < right.Step;

    public static bool operator <=(ProcessStepValue left, ProcessStepValue right) => left.Step <= right.Step;

    #endregion
    
    #region Implementation of IDecrementOperators<ProcessStepValue>
    
    public static ProcessStepValue operator --(ProcessStepValue value) => value with { Step = value.Step - 1 };

    #endregion

    #region Implementation of IDivisionOperators<ProcessStepValue,ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator /(ProcessStepValue left, ProcessStepValue right) => left with { Step = left.Step / right.Step };

    #endregion
    
    #region Implementation of IIncrementOperators<ProcessStepValue>
    
    public static ProcessStepValue operator ++(ProcessStepValue value) => value with { Step = value.Step + 1 };

    #endregion
    
    #region Implementation of IModulusOperators<ProcessStepValue,ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator %(ProcessStepValue left, ProcessStepValue right) => left with { Step = left.Step % right.Step };

    #endregion
    
    #region Implementation of IMultiplicativeIdentity<ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue MultiplicativeIdentity => new(1, string.Empty);
    
    #endregion
    
    #region Implementation of IMultiplyOperators<ProcessStepValue,ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator *(ProcessStepValue left, ProcessStepValue right) => left with { Step = left.Step * right.Step };

    #endregion
    
    #region Implementation of ISubtractionOperators<ProcessStepValue,ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator -(ProcessStepValue left, ProcessStepValue right) => left with { Step = left.Step - right.Step };

    #endregion
    
    #region Implementation of IUnaryNegationOperators<ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator -(ProcessStepValue value) => value with { Step = -value.Step };

    #endregion
    
    #region Implementation of IUnaryPlusOperators<ProcessStepValue,ProcessStepValue>
    
    public static ProcessStepValue operator +(ProcessStepValue value) => value;

    #endregion
    
    #region Implementation of INumberBase<ProcessStepValue>
    
    public static ProcessStepValue Abs(ProcessStepValue value) => value with { Step = Math.Abs(value.Step) };

    public static bool IsCanonical(ProcessStepValue value) => true;
    public static bool IsComplexNumber(ProcessStepValue value) => false;
    public static bool IsEvenInteger(ProcessStepValue value) => value.Step % 2 == 0;
    public static bool IsFinite(ProcessStepValue value) => true;
    public static bool IsImaginaryNumber(ProcessStepValue value) => false;
    public static bool IsInfinity(ProcessStepValue value) => false;
    public static bool IsInteger(ProcessStepValue value) => true;
    public static bool IsNaN(ProcessStepValue value) => false;
    public static bool IsNegative(ProcessStepValue value) => value.Step < 0;
    public static bool IsNegativeInfinity(ProcessStepValue value) => false;
    public static bool IsNormal(ProcessStepValue value) => true;
    public static bool IsOddInteger(ProcessStepValue value) => value.Step % 2 != 0;
    public static bool IsPositive(ProcessStepValue value) => value.Step > 0;
    public static bool IsPositiveInfinity(ProcessStepValue value) => false;
    public static bool IsRealNumber(ProcessStepValue value) => true;
    public static bool IsSubnormal(ProcessStepValue value) => false;
    public static bool IsZero(ProcessStepValue value) => value.Step == 0;
    public static ProcessStepValue MaxMagnitude(ProcessStepValue x, ProcessStepValue y)
    {
        return x with { Step = Math.Max(Math.Abs(x.Step), Math.Abs(y.Step)) };
    }
    
    public static ProcessStepValue MaxMagnitudeNumber(ProcessStepValue x, ProcessStepValue y) => MaxMagnitude(x, y);

    public static ProcessStepValue MinMagnitude(ProcessStepValue x, ProcessStepValue y) => x with { Step = Math.Min(Math.Abs(x.Step), Math.Abs(y.Step)) };

    public static ProcessStepValue MinMagnitudeNumber(ProcessStepValue x, ProcessStepValue y) => MinMagnitude(x, y);

    public static ProcessStepValue Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => new(int.Parse(s, style, provider), string.Empty);

    public static ProcessStepValue Parse(string s, NumberStyles style, IFormatProvider? provider) => new(int.Parse(s, style, provider), string.Empty);

    public static bool TryConvertFromChecked<TOther>(TOther value, out ProcessStepValue result) where TOther : INumberBase<TOther>
    {
        if (TOther.TryConvertToChecked(value, out int intValue))
        {
            result = new ProcessStepValue(intValue, string.Empty);
            return true;
        }
        
        result = default;
        return false;
    }
    
    public static bool TryConvertFromSaturating<TOther>(TOther value, out ProcessStepValue result) where TOther : INumberBase<TOther>
    {
        if (TOther.TryConvertToSaturating(value, out int intValue))
        {
            result = new ProcessStepValue(intValue, string.Empty);
            return true;
        }
        result = default;
        return false;
    }
    
    public static bool TryConvertFromTruncating<TOther>(TOther value, out ProcessStepValue result) where TOther : INumberBase<TOther>
    {
        if (TOther.TryConvertToTruncating(value, out int intValue))
        {
            result = new ProcessStepValue(intValue, string.Empty);
            return true;
        }
        result = default;
        return false;
    }
    
    public static bool TryConvertToChecked<TOther>(ProcessStepValue value, out TOther result) where TOther : INumberBase<TOther> => TOther.TryConvertFromChecked(value.Step, out result!);

    public static bool TryConvertToSaturating<TOther>(ProcessStepValue value, out TOther result) where TOther : INumberBase<TOther> => TOther.TryConvertFromSaturating(value.Step, out result!);

    public static bool TryConvertToTruncating<TOther>(ProcessStepValue value, out TOther result) where TOther : INumberBase<TOther> => TOther.TryConvertFromTruncating(value.Step, out result!);

    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out ProcessStepValue result)
    {
        if (int.TryParse(s, style, provider, out var stepValue))
        {
            result = new ProcessStepValue(stepValue, string.Empty);
            return true;
        }
        
        result = default;
        return false;
    }
    
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out ProcessStepValue result)
    {
        if (int.TryParse(s, style, provider, out var stepValue))
        {
            result = new ProcessStepValue(stepValue, string.Empty);
            return true;
        }
        
        result = default;
        return false;
    }
    
    public static ProcessStepValue One => new(1, string.Empty);
    
    public static int Radix => 2;
    
    public static ProcessStepValue Zero => new(0, string.Empty);
    
    #endregion
    
    #endregion
}