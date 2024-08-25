/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using System.Collections.Generic;
using OneScript.Contexts;
using OneScript.Types;
using OneScript.Debug.Grpc;
using OneScript.Values;

namespace ScriptEngine.Debugging
{
    public class DefaultVariableVisualizer : IVariableVisualizer
    {
        private readonly ITypeManager _typeManager;

        public DefaultVariableVisualizer(ITypeManager typeManager) 
        {
            _typeManager = typeManager;
        }

        public OsVariable GetVariable(IVariable value, int index)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            
            string presentation;
            string typeName;

            //На случай проблем, подобных:
            //https://github.com/EvilBeaver/OneScript/issues/918

            try
            {
                presentation = value.AsString();
            }
            catch (Exception e)
            {
                presentation = $"Ошибка получения значения: {e.Message}";
            }

            try
            {
                typeName = value.SystemType.Name;
            }
            catch (Exception e)
            {
                if (_typeManager.TryGetType(value.Value.GetType(), out var typeDescriptor))
                    typeName = typeDescriptor.Name;
                else
                    typeName = $"Ошибка получения типа: {e.Message}";
            }

            return new OsVariable()
            {
                Name = value.Name,
                Type = typeName,
                Value = presentation,
                Index = index,
                IsStructured = IsStructured(value.Value)
            };
        }

        public IReadOnlyList<IVariable> GetChildVariables(IValue value)
        {
            var presenter = new DefaultValueVisitor();
            
            if (value.GetRawValue() is IRuntimeContextInstance)
            {
                var objectValue = value.AsObject();
                if (objectValue is IDebugPresentationAcceptor customPresenter)
                {
                    customPresenter.Accept(presenter);
                }
                else
                {
                    if (HasProperties(objectValue))
                    {
                        presenter.ShowProperties(objectValue);
                    }

                    if (HasIndexes(objectValue as ICollectionContext<IValue>))
                    {
                        var context = value.AsObject();
                        if (context is IEnumerable<IValue> collection)
                        {
                            presenter.ShowCollectionItems(collection);
                        }
                    }
                }
            }
            
            return presenter.Result;
        }

        private static bool IsStructured(IValue value)
        {
            return HasProperties(value as IRuntimeContextInstance) 
                   || HasIndexes(value as ICollectionContext<IValue>);
        }

        private static bool HasIndexes(ICollectionContext<IValue> collection)
        {
            return collection?.Count() > 0;
        }

        private static bool HasProperties(IRuntimeContextInstance value)
        {
            return value?.GetPropCount() > 0;
        }
    }
}