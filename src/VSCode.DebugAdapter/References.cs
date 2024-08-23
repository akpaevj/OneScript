/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System.Collections.Generic;

namespace VSCode.DebugAdapter
{
    class References<T>
    {
        private readonly object _locker = new();
        private readonly List<T> _items = new();

        public int Add(T item)
        {
            lock (_locker)
            {
                _items.Add(item);
                return _items.Count;
            }
        }

        public T Get(int reference)
        {
            lock (_locker)
                return _items[reference - 1];
        }

        public bool Has(int reference)
        {
            lock (_locker)
                return _items.Count > reference;
        }
    }
}
