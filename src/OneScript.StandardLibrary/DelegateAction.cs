/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;
using System.Linq;
using OneScript.Contexts;
using OneScript.Types;
using OneScript.Values;
using ScriptEngine.Machine;
using ScriptEngine.Machine.Contexts;

namespace OneScript.StandardLibrary
{
    /// <summary>
    /// Делегат для выполнения метода в другом объекте
    /// </summary>
    [ContextClass("Действие","Action")]
    public class DelegateAction : ContextIValueImpl
    {
        private readonly Func<IValue[], IValue> _action;
        private const string MethodName_Ru = "Выполнить";
        private const string MethodName_En = "Execute";

        private static readonly BslMethodInfo _executeMethodInfo;

        static DelegateAction()
        {
            var builder = BslMethodBuilder.Create()
                .DeclaringType(typeof(DelegateAction))
                .ReturnType(typeof(BslValue))
                .SetNames(MethodName_Ru, MethodName_En);

            _executeMethodInfo = builder.Build();
        }
        
        public DelegateAction(ITypeManager typeManager, Func<IValue[], IValue> action) : base(typeManager)
        {
            _action = action;
        }

        public DelegateAction(ITypeManager typeManager, Func<BslValue[], BslValue> action) : base(typeManager)
        {
            _action = parameters => action( parameters.Select(x=>x.GetRawValue())
                .Cast<BslValue>().ToArray() );
        }
        
        public override bool DynamicMethodSignatures => true;

        public override int GetMethodNumber(string name)
        {
            if (string.Compare(name, MethodName_En, StringComparison.OrdinalIgnoreCase) == 0
                || string.Compare(name, MethodName_Ru, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return 0;
            }

            return base.GetMethodNumber(name);
        }

        public override int GetMethodsCount()
        {
            return 1;
        }

        public override BslMethodInfo GetMethodInfo(int methodNumber)
        {
            return _executeMethodInfo;
        }

        public override void CallAsFunction(int methodNumber, IValue[] arguments, out IValue retValue)
        {
            retValue = _action(arguments);
        }

        public override void CallAsProcedure(int methodNumber, IValue[] arguments)
        {
            _action(arguments);
        }

        [ScriptConstructor]
        public static DelegateAction Create(TypeActivationContext typeActivationContext, IRuntimeContextInstance target, string methodName)
        {
            var typeManager = typeActivationContext.Services.Resolve<ITypeManager>();

            var method = target.GetMethodNumber(methodName);

            IValue action(IValue[] parameters)
            {
                target.CallAsFunction(method, parameters, out var retVal);
                return retVal;
            }

            return new DelegateAction(typeManager, action);
        }
    }
}