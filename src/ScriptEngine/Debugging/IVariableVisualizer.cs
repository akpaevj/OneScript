/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System.Collections.Generic;
using OneScript.Contexts;
using ScriptEngine.Debugging.Grpc;
using ScriptEngine.Machine;

namespace ScriptEngine.Debugging
{
    public interface IVariableVisualizer
    {
        OsVariable GetVariable(IVariable value, int index);
        
        IReadOnlyList<IVariable> GetChildVariables(IValue value);
    }
}