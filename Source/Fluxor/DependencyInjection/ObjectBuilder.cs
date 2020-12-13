﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Fluxor.DependencyInjection
{
	internal interface IObjectBuilder
	{
		object Build(Type type);
		void Register<T>(T instance);
	}

	internal class ObjectBuilder : IObjectBuilder
	{
		private readonly object SyncRoot = new object();
		private IServiceProvider ServiceProvider;
		private Dictionary<Type, object> Cache = new Dictionary<Type, object>();

		public ObjectBuilder(IServiceProvider serviceProvider)
		{
			ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		}

		public object Build(Type type)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (!type.IsClass || type.IsAbstract)
				throw new ArgumentException($"Type '{type.FullName}' must be a concrete class", nameof(type));

			lock (SyncRoot)
			{
				var buildPath = new Stack<Type>();
				return Build(type, buildPath);
			}
		}

		public void Register<T>(T instance)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));
			Cache.Add(typeof(T), instance);
		}

		private object Build(Type type, Stack<Type> buildPath)
		{
			if (Cache.TryGetValue(type, out object result))
			{
				// TODO: PeteM - D1
				Console.WriteLine("Cached: " + type.FullName);
				return result;
			}
			// TODO: PeteM - DB
			{
				Console.WriteLine("Building: " + type.FullName);
				foreach (Type cached in Cache.Keys)
					Console.WriteLine("  Cached=" + cached.FullName);
			}

			if (buildPath.Contains(type))
			{
				string path = string.Join(" -> ", buildPath.Reverse().Select(x => x.Name));
				throw new InvalidOperationException(
					$"A circular dependency was detected for the service of type '{type.FullName}'" +
					$"\r\n{path}");
			}

			buildPath.Push(type);

			ConstructorInfo constructor = GetGreediestConstructor(type);
			if (constructor == null)
				throw new ArgumentException($"Type '{type.FullName}' has no constructor", nameof(type));

			Type[] parameterTypes = constructor.GetParameters().Select(x => x.ParameterType).ToArray();
			var parameterValues = new List<object>();

			foreach (Type parameterType in parameterTypes)
			{
				object value = ServiceProvider.GetService(parameterType);
				if (value == null)
					value = Build(parameterType, buildPath);
				parameterValues.Add(value);
			}

			result = constructor.Invoke(parameterValues.ToArray());
			Cache[type] = result;

			buildPath.Pop();
			return result;
		}

		private static ConstructorInfo GetGreediestConstructor(Type type) =>
			type.GetConstructors().OrderByDescending(x => x.GetParameters().Length).FirstOrDefault();
	}
}
