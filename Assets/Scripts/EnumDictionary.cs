using System;
using System.Collections.Generic;

/// <summary>
/// Dictionary helper for maps where every declared enum value should have an entry.
/// </summary>
[Serializable]
public class EnumDictionary<TEnum, TValue> : Dictionary<TEnum, TValue> where TEnum : struct, Enum
{
    public EnumDictionary() : base(GetEnumValueCount())
    {
    }

    public EnumDictionary(TValue defaultValue) : this()
    {
        SetAll(defaultValue);
    }

    public EnumDictionary(Func<TEnum, TValue> valueFactory) : this()
    {
        SetAll(valueFactory);
    }

    public void SetAll(TValue value)
    {
        foreach (TEnum key in GetEnumValues())
        {
            this[key] = value;
        }
    }

    public void SetAll(Func<TEnum, TValue> valueFactory)
    {
        if (valueFactory == null)
        {
            throw new ArgumentNullException(nameof(valueFactory));
        }

        foreach (TEnum key in GetEnumValues())
        {
            this[key] = valueFactory(key);
        }
    }

    public bool ContainsAllEnumKeys()
    {
        foreach (TEnum key in GetEnumValues())
        {
            if (!ContainsKey(key))
            {
                return false;
            }
        }

        return true;
    }

    public void FillMissing(TValue defaultValue)
    {
        foreach (TEnum key in GetEnumValues())
        {
            if (!ContainsKey(key))
            {
                this[key] = defaultValue;
            }
        }
    }

    public void FillMissing(Func<TEnum, TValue> valueFactory)
    {
        if (valueFactory == null)
        {
            throw new ArgumentNullException(nameof(valueFactory));
        }

        foreach (TEnum key in GetEnumValues())
        {
            if (!ContainsKey(key))
            {
                this[key] = valueFactory(key);
            }
        }
    }

    private static TEnum[] GetEnumValues()
    {
        return (TEnum[])Enum.GetValues(typeof(TEnum));
    }

    private static int GetEnumValueCount()
    {
        return GetEnumValues().Length;
    }
}
