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

	public static TViewModel CreateProxy<TViewModel>(IViewModelProxy<TViewModel> viewModel)
		where TViewModel : class
	{
		var interceptorTemplate = (_interceptorTemplateCache.GetValue(typeof(TViewModel)) as InterceptorTemplate<TViewModel>) ?? throw new NotImplementedException();
		var interceptorWrapper = new InterceptorWrapper<TViewModel>(viewModel, interceptorTemplate);
		return _proxyGenerator.CreateInterfaceProxyWithoutTarget<TViewModel>(interceptorWrapper);
	}

	private static object CreateInterceptorTemplate(Type t)
	{
		return Activator.CreateInstance(typeof(InterceptorTemplate<>).MakeGenericType([t])) ?? throw new NotImplementedException();
	}

	#region InterceptorWrapper
	private sealed class InterceptorWrapper<T>(IViewModelProxy<T> data, InterceptorTemplate<T> interceptorTemplate) : IInterceptor where T : class
	{
		private readonly IViewModelProxy<T> _data = data ?? throw new ArgumentNullException(nameof(data));
		private readonly InterceptorTemplate<T> _interceptorTemplate = interceptorTemplate ?? throw new ArgumentNullException(nameof(interceptorTemplate));
		public void Intercept(IInvocation invocation) => _interceptorTemplate.Intercept(invocation, _data);
	}
	#endregion InterceptorWrapper

	#region InterceptorTemplate
	private sealed class InterceptorTemplate<T> where T : class
	{
		private record struct InvocationIdentity(string MethodName, string DeclaringTypeFullName);
		private readonly Dictionary<string, Action<IViewModelProxy<T>, IInvocation>> _invocationCallbackCache = [];

		public InterceptorTemplate()
		{
			var targetTypes = GetValidTargetTypes(typeof(T)).ToList();
			var notifyPropertyChanged = targetTypes.Remove(typeof(INotifyPropertyChanged))
				? typeof(INotifyPropertyChanged).GetEvent("PropertyChanged")
				: null;
			var notifyPropertyChanging = targetTypes.Remove(typeof(INotifyPropertyChanging))
				? typeof(INotifyPropertyChanging).GetEvent("PropertyChanging")
				: null;

			if (notifyPropertyChanged is not null)
			{
				var adder = notifyPropertyChanged.GetAddMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[adder.Name] = (data, inv) =>
				{
					if (inv.Arguments[0] is PropertyChangedEventHandler handler
						&& data is INotifyPropertyChanged viewModel)
					{
						viewModel.PropertyChanged += handler!;
					}
				};

				var remover = notifyPropertyChanged.GetRemoveMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[remover.Name] = (data, inv) =>
				{
					if (inv.Arguments[0] is PropertyChangedEventHandler handler
						&& data is INotifyPropertyChanged viewModel)
					{
						viewModel.PropertyChanged -= handler;
					}
				};
			}

			if (notifyPropertyChanging is not null)
			{
				var adder = notifyPropertyChanging.GetAddMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[adder.Name] = (data, inv) =>
				{
					if (inv.Arguments[0] is PropertyChangingEventHandler handler
						&& data is INotifyPropertyChanging viewModel)
					{
						viewModel.PropertyChanging += handler;
					}
				};

				var remover = notifyPropertyChanging.GetRemoveMethod() ?? throw new NotImplementedException();
				_invocationCallbackCache[remover.Name] = (data, inv) =>
				{
					if (inv.Arguments[0] is PropertyChangingEventHandler handler
						&& data is INotifyPropertyChanging viewModel)
					{
						viewModel.PropertyChanging -= handler;
					}
				};
			}

			var properties = targetTypes.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
			foreach (var property in properties)
			{
				var getter = property.GetGetMethod();
				if (getter is not null)
				{
					_invocationCallbackCache[getter.Name] = CreateGetterInvocationCallback(property);
				}

				var setter = property.GetSetMethod();
				if (setter is not null)
				{
					_invocationCallbackCache[setter.Name] = CreateSetterInvocationCallback(property);
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

		private static Action<IViewModelProxy<T>, IInvocation> CreateGetterInvocationCallback(PropertyInfo propertyInfo)
		{
			var propertyName = propertyInfo.Name;
			if (!propertyInfo.CanRead)
			{
				throw new ReadOnlyPropertyException(propertyName);
			}

			return (data, inv) =>
			{
				inv.ReturnValue = data.GetValue(
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

		private static Action<IViewModelProxy<T>, IInvocation> CreateSetterInvocationCallback(PropertyInfo propertyInfo)
		{
			var propertyName = propertyInfo.Name;

			if (!propertyInfo.CanWrite)
			{
				throw new ReadOnlyPropertyException(propertyName);
			}

			var propertyType = propertyInfo.PropertyType;
			return (data, inv) =>
			{
				var val = inv.Arguments[0];
				if (val is not null && !propertyType.IsAssignableFrom(val.GetType()))
				{
					throw new InvalidCastException($"Cannot assign value of type '{val.GetType().FullName ?? "null"}' to property '{propertyName}' of type '{propertyType}'.");
				}

				data.SetValue(propertyName, val);
			};
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

		public void Intercept(IInvocation invocation, IViewModelProxy<T> data)
		{
			if (_invocationCallbackCache.TryGetValue(invocation.Method.Name, out var invocationCallback))
			{
				invocationCallback?.Invoke(data, invocation);
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
