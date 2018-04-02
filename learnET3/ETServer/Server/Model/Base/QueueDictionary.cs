﻿using System.Collections.Generic;

namespace ETModel
{
    /// <summary>
    /// 保存key到队列里的字典
    /// </summary>
	public class QueueDictionary<T, K>
	{
		private readonly List<T> list = new List<T>();
		private readonly Dictionary<T, K> dictionary = new Dictionary<T, K>();

        //添加到队列
		public void Enqueue(T t, K k)
		{
			this.list.Add(t);
			this.dictionary.Add(t, k);
		}

        //从队列中移除第一个
		public void Dequeue()
		{
			if (this.list.Count == 0)
			{
				return;
			}
			T t = this.list[0];
			this.list.RemoveAt(0);
			this.dictionary.Remove(t);
		}

		public void Remove(T t)
		{
			this.list.Remove(t);
			this.dictionary.Remove(t);
		}

		public bool ContainsKey(T t)
		{
			return this.dictionary.ContainsKey(t);
		}

		public int Count
		{
			get
			{
				return this.list.Count;
			}
		}

		public T FirstKey
		{
			get
			{
				return this.list[0];
			}
		}
		
		public K FirstValue
		{
			get
			{
				T t = this.list[0];
				return this[t];
			}
		}

		public K this[T t]
		{
			get
			{
				return this.dictionary[t];
			}
		}

		public void Clear()
		{
			this.list.Clear();
			this.dictionary.Clear();
		}
	}
}