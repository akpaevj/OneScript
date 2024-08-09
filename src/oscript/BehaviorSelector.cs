﻿/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.Text;

namespace oscript
{
    static class BehaviorSelector
    {
        public static AppBehavior Select(string[] cmdLineArgs)
        {
            var helper = new CmdLineHelper(cmdLineArgs);
            string arg = helper.Next();

            if(arg == null)
                return new ShowUsageBehavior();

            if (!arg.StartsWith("-"))
            {
                var path = arg;
                return new ExecuteScriptBehavior(path, helper.Tail());
            }

            var selected = SelectParametrized(helper);
            selected ??= new ShowUsageBehavior();

            return selected;
            
        }

        private static AppBehavior SelectParametrized(CmdLineHelper helper)
        {
            var initializers = new Dictionary<string, Func<CmdLineHelper, AppBehavior>>
            {
                { "-measure", MeasureBehavior.Create },
                { "-compile", ShowCompiledBehavior.Create },
                { "-check", CheckSyntaxBehavior.Create },
                { "-version", h => new ShowVersionBehavior() },
                { "-v", h => new ShowVersionBehavior() },
                { "-encoding", ProcessEncodingKey },
                { "-codestat", EnableCodeStatistics },
                { "-debug", DebugBehavior.Create }
            };

            var param = helper.Parse(helper.Current());
            if(initializers.TryGetValue(param.Name.ToLowerInvariant(), out var action))
            {
                return action(helper);
            }

            return null;
        }

        private static AppBehavior EnableCodeStatistics(CmdLineHelper helper)
        {
            var param = helper.Parse(helper.Current());
            if (string.IsNullOrEmpty(param.Value))
                return null;
            
            var outputStatFile = param.Value;

            var behavior = Select(helper.Tail());
            if (behavior is ExecuteScriptBehavior executor)
                executor.CodeStatFile = outputStatFile;

            return behavior;
        }
        
        private static AppBehavior ProcessEncodingKey(CmdLineHelper helper)
        {
            var param = helper.Parse(helper.Current());
            if (!string.IsNullOrEmpty(param.Value))
            {
                var encValue = param.Value;
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding(encValue);
                }
                catch
                {
                    Output.WriteLine("Wrong console encoding");
                    encoding = null;
                }

                if (encoding != null)
                    Program.ConsoleOutputEncoding = encoding;

                return Select(helper.Tail());
            }

            return null;
        }
    }

}
