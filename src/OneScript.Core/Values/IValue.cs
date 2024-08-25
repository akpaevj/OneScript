/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using OneScript.Commons;
using OneScript.Contexts;
using OneScript.Exceptions;
using OneScript.Types;

namespace OneScript.Values
{
    public interface IValue : IComparable<IValue>, IEquatable<IValue>
    {
        //DataType DataType { get; }
        TypeDescriptor SystemType { get; }

        // TODO: Избавиться нахер от этого ужоса
        IValue GetRawValue();

        public bool AsBoolean() => (bool)(BslValue)GetRawValue();
        public DateTime AsDate() => (DateTime)(BslValue)GetRawValue();
        public decimal AsNumber() => (decimal)(BslValue)GetRawValue();
        public string AsString() => (string)(BslValue)GetRawValue();

        public IRuntimeContextInstance AsObject()
            => GetRawValue() is IRuntimeContextInstance ctx ? ctx : throw BslExceptions.ValueIsNotObjectException();

        public object ConvertToClrObject()
        {
            if (this == null)
                return null;

            var raw = GetRawValue();
            return raw switch
            {
                BslNumericValue num => (decimal)num,
                BslBooleanValue boolean => (bool)boolean,
                BslStringValue str => (string)str,
                BslDateValue date => (DateTime)date,
                BslUndefinedValue _ => null,
                BslNullValue _ => null,
                BslTypeValue type => type.SystemType.ImplementingClass,
                IObjectWrapper wrapper => wrapper.UnderlyingObject,
                BslObjectValue obj => obj,
                _ => throw ValueMarshallingException.NoConversionToCLR(raw.GetType())
            };
        }

        public bool IsSkippedArgument()
        {
            return ReferenceEquals(this, BslSkippedParameterValue.Instance);
        }
    }
}