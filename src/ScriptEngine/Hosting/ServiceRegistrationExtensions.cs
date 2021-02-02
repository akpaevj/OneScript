﻿/*----------------------------------------------------------
This Source Code Form is subject to the terms of the
Mozilla Public License, v.2.0. If a copy of the MPL
was not distributed with this file, You can obtain one
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/

using System;

namespace ScriptEngine.Hosting
{
    public static class ServiceRegistrationExtensions
    {
        public static IEngineBuilder SetupConfiguration(this IEngineBuilder b, Action<ConfigurationProviders> setup)
        {
            setup(b.ConfigurationProviders);
            b.Services.RegisterSingleton(b.ConfigurationProviders);
            return b;
        }
    }
}