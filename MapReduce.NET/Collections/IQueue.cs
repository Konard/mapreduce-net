﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapReduce.NET.Collections
{
    public interface IQueue<K,V>
    {
        void Push(K key, V value);
    }
}
