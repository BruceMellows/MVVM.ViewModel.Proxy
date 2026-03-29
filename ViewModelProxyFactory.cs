// MIT License
//
// Copyright (c) 2026 BruceMellows
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using BruceMellows.EventuallyCorrect;
using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public static class ViewModelProxyFactory
{
	private static readonly ProxyGenerator _proxyGenerator = new();
	private static readonly EventuallyCorrectCache<Type, object> _interceptorTemplateCache = new(CreateInterceptorTemplate, TypeComparer.Instance);

	public static TViewModel CreateProxy<TViewModel>(IViewModelProxy<TViewModel> viewModelProxy)
		where TViewModel : class
	{
		var interceptorTemplate = (_interceptorTemplateCache.GetValue(typeof(TViewModel)) as InterceptorTemplate<TViewModel>) ?? throw new NotImplementedException();
		var interceptorWrapper = new InterceptorWrapper<TViewModel>(viewModelProxy, interceptorTemplate);
		return _proxyGenerator.CreateInterfaceProxyWithoutTarget<TViewModel>(interceptorWrapper);
	}

	private static object CreateInterceptorTemplate(Type t)
	{
		return Activator.CreateInstance(typeof(InterceptorTemplate<>).MakeGenericType([t])) ?? throw new NotImplementedException();
	}

	#region InterceptorWrapper
	private sealed class InterceptorWrapper<TViewModel>(IViewModelProxy<TViewModel> viewModelProxy, InterceptorTemplate<TViewModel> interceptorTemplate) : IInterceptor where TViewModel : class
	{
		private readonly IViewModelProxy<TViewModel> _viewModelProxy = viewModelProxy ?? throw new ArgumentNullException(nameof(viewModelProxy));
		private readonly InterceptorTemplate<TViewModel> _interceptorTemplate = interceptorTemplate ?? throw new ArgumentNullException(nameof(interceptorTemplate));
		public void Intercept(IInvocation invocation) => _interceptorTemplate.Intercept(invocation, _viewModelProxy);
	}
	#endregion InterceptorWrapper

	#region InterceptorTemplate
	private sealed class InterceptorTemplate<TViewModel> where TViewModel : class
	{
		private record struct InvocationIdentity(string MethodName, string DeclaringTypeFullName);
		private readonly Dictionary<string, Action<IViewModelProxy<TViewModel>, IInvocation>> _invocationCallbackCache = [];

		public InterceptorTemplate()
		{
			var viewModelTypes = GetValidTargetTypes(typeof(TViewModel)).ToList();
			var eventInfos = viewModelTypes
				.SelectMany(t => t.GetEvents(BindingFlags.Public | BindingFlags.Instance))
				.Where(e => e.EventHandlerType is not null)
				.ToArray();

			foreach (var eventInfo in eventInfos)
			{
				var adder = eventInfo.GetAddMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[adder.Name] = (viewModelProxy, inv) =>
				{
					var declaringType = inv.Method.DeclaringType
						?? throw new NotImplementedException();

					var viewModelProxyType = viewModelProxy.GetType();
					if (GetAllInterfaces(viewModelProxyType).All(i => i != declaringType))
					{
						throw MissingEventInterfaceException.WhenAttaching(declaringType, viewModelProxyType, eventInfo);
					}

					if (inv.Arguments[0] is not Delegate eventHandler)
					{
						throw new NotImplementedException();
					}

					eventInfo.AddEventHandler(viewModelProxy, eventHandler);
				};

				var remover = eventInfo.GetRemoveMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[remover.Name] = (viewModelProxy, inv) =>
				{
					var declaringType = inv.Method.DeclaringType
						?? throw new NotImplementedException();

					var viewModelProxyType = viewModelProxy.GetType();
					if (GetAllInterfaces(viewModelProxyType).All(i => i != declaringType))
					{
						throw MissingEventInterfaceException.WhenDetaching(declaringType, viewModelProxyType, eventInfo);
					}

					if (inv.Arguments[0] is not Delegate eventHandler)
					{
						throw new NotImplementedException();
					}

					eventInfo.RemoveEventHandler(viewModelProxy, eventHandler);
				};
			}

			var propertyInfos = viewModelTypes.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
			foreach (var propertyInfo in propertyInfos)
			{
				var getter = propertyInfo.GetGetMethod();
				if (getter is not null)
				{
					_invocationCallbackCache[getter.Name] = CreateGetterInvocationCallback(propertyInfo);
				}

				var setter = propertyInfo.GetSetMethod();
				if (setter is not null)
				{
					_invocationCallbackCache[setter.Name] = CreateSetterInvocationCallback(propertyInfo);
				}
			}
		}

		private static Type EnsureValidTargetType(Type targetType)
		{
			InvalidTargetTypeException.ThrowIfInvalid(targetType);
			return targetType;
		}

		private static IEnumerable<Type> GetValidTargetTypes(Type targetType)
		{
			return targetType
				.GetInterfaces()
				.SelectMany(GetValidTargetTypes)
				.Append(targetType)
				.GroupBy(t => t.FullName)
				.Select(g => EnsureValidTargetType(g.First()));
		}

		private static Action<IViewModelProxy<TViewModel>, IInvocation> CreateGetterInvocationCallback(PropertyInfo propertyInfo)
		{
			var propertyName = propertyInfo.Name;
			if (!propertyInfo.CanRead)
			{
				throw new ReadOnlyPropertyException(propertyName);
			}

			return (viewModelProxy, inv) =>
			{
				inv.ReturnValue = viewModelProxy.GetValue(
					propertyName,
					() =>
					{
						var defaultAttribute = propertyInfo.GetCustomAttributes()
							.OfType<IDefaultValue>()
							.SingleOrDefault()
							?? throw new NoInitialValueException(propertyName);
						return defaultAttribute.Value;
					});
			};
		}

		private static Action<IViewModelProxy<TViewModel>, IInvocation> CreateSetterInvocationCallback(PropertyInfo propertyInfo)
		{
			var propertyName = propertyInfo.Name;

			if (!propertyInfo.CanWrite)
			{
				throw new ReadOnlyPropertyException(propertyName);
			}

			var propertyType = propertyInfo.PropertyType;
			return (viewModelProxy, inv) =>
			{
				var val = inv.Arguments[0];
				if (val is not null && !propertyType.IsAssignableFrom(val.GetType()))
				{
					throw new InvalidCastException($"Cannot assign value of type '{val.GetType().FullName ?? "null"}' to property '{propertyName}' of type '{propertyType}'.");
				}

				viewModelProxy.SetValue(propertyName, val);
			};
		}

		private static HashSet<Type> GetAllInterfaces(Type? type)
		{
			if (type == null) throw new ArgumentNullException(nameof(type));

			var interfaces = new HashSet<Type>();

			while (type != null)
			{
				foreach (var iface in type.GetInterfaces())
				{
					if (!interfaces.Contains(iface))
					{
						interfaces.Add(iface);

						// Recursively get all nested interfaces
						foreach (var nestedIface in GetAllInterfaces(iface))
						{
							if (!interfaces.Contains(nestedIface))
							{
								interfaces.Add(nestedIface);
							}
						}
					}
				}

				type = type.BaseType;
			}

			return interfaces;
		}
		private sealed class InvocationComparer : IEqualityComparer<IInvocation>
		{
			public static readonly InvocationComparer Instance = new();

			public bool Equals(IInvocation? lhs, IInvocation? rhs)
			{
				var lhsDesc = lhs?.Method.ToString();
				var rhsDesc = rhs?.Method.ToString();
				return ReferenceEquals(lhsDesc, rhsDesc)
					? true
					: lhs is null || rhs is null
						? false
						: lhs.Method.ToString() == rhs.Method.ToString();
			}

			public int GetHashCode(IInvocation obj) => obj.Method.ToString()?.GetHashCode() ?? throw new NotImplementedException();

			public static string CreateIdentity(IInvocation invocation) => invocation.Method.ToString() ?? throw new NotImplementedException();
		}

		public void Intercept(IInvocation invocation, IViewModelProxy<TViewModel> viewModelProxy)
		{
			if (_invocationCallbackCache.TryGetValue(invocation.Method.Name, out var invocationCallback))
			{
				invocationCallback?.Invoke(viewModelProxy, invocation);
			}
		}
	}
	#endregion InterceptorTemplate

	#region TypeComparer
	private sealed class TypeComparer : IEqualityComparer<Type>
	{
		public static TypeComparer Instance { get; } = new();
		public bool Equals(Type? x, Type? y) => ReferenceEquals(x, y) || (x is not null && x.FullName == y!.FullName);
		public int GetHashCode(Type obj) => RuntimeHelpers.GetHashCode(obj);
	}
	#endregion TypeComparer
}
