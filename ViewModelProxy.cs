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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public abstract class ViewModelProxy<TViewModel> : IViewModelProxy<TViewModel>
	where TViewModel : class
{
	#region fields
	private readonly TViewModel? _viewModelProxy;
	private readonly Dictionary<string, object?> _properties = [];
	private readonly ConcurrentDictionary<string, IPropertyBinding> bindings = [];
	private readonly ThreadLocal<HashSet<string>> _currentBindings = new(() => []);

	#endregion fields

	public ViewModelProxy()
	{
		_viewModelProxy = ViewModelProxyFactory.CreateProxy(this);
	}

	protected void OnChanged<TResult>(Expression<Func<TViewModel, TResult>> expression, Action<TViewModel, TResult> callback)
	{
		var propertyName = (expression.Body as MemberExpression)?.Member.Name
			?? throw new ArgumentException("Expression must be a member expression.", nameof(expression));
		bindings[propertyName] = new PropertyBinding<TResult>(callback);
	}

	#region Internals

	#region IViewModelProxy<TViewModel>
	public TViewModel ViewModel => _viewModelProxy!;

	public object? GetValue(string key, Func<object> getDefaultValue)
	{
		return _properties.TryGetValue(key, out var value) || SetValue(key, value = getDefaultValue())
			? value
			: null;
	}

	public bool SetValue(string key, object? value)
	{

		using var bindingRecursionDetection = DoDoneAction.Create(
			() =>
			{
				if (!_currentBindings.Value!.Add(key))
				{
					throw new InvalidOperationException($"Circular binding detected for properties: {string.Join(", ", _currentBindings.Value)}");
				}
			},
			() =>
			{
				if (!_currentBindings.Value!.Remove(key))
				{
					throw new InvalidOperationException($"Property not found: {key}");
				}
			});

		using var propChanging = DoDoneAction.Create(
			() =>
			{
				OnPropertyChangeBegin(key, ViewModel);
				RaiseEvent<INotifyPropertyChanging>(nameof(INotifyPropertyChanging.PropertyChanging), ViewModel, new PropertyChangingEventArgs(key));
			},
			() => OnPropertyChangeEnd(key, ViewModel));

		if (_properties.TryGetValue(key, out var oldValue) && Equals(oldValue, value))
		{
			return false;
		}

		_properties[key] = value;
		OnPropertyChanged(key, ViewModel);
		RaiseEvent<INotifyPropertyChanged>(nameof(INotifyPropertyChanged.PropertyChanged), ViewModel, new PropertyChangedEventArgs(key));

		if (bindings.TryGetValue(key, out var binding))
		{
			binding.Invoke(ViewModel, value!);
		}

		return true;
	}

	public void OnPropertyChangeBegin(string key, TViewModel viewModel)
	{
	}

	public void OnPropertyChanged(string key, TViewModel viewModel)
	{
	}

	public void OnPropertyChangeEnd(string key, TViewModel viewModel)
	{
	}
	#endregion IViewModelProxy<TViewModel>

	#region Binding implementation
	private interface IPropertyBinding
	{
		void Invoke(TViewModel viewModel, object value);
	}

	private class PropertyBinding<TResult>(Action<TViewModel, TResult> callback) : IPropertyBinding
	{
		private Action<TViewModel, TResult> callback = callback;
		public void Invoke(TViewModel viewModel, object? value) => callback(viewModel, (TResult)value!);
	}
	#endregion Binding implementation

	private void RaiseEvent<T>(string eventName, object sender, object eventArgs)
	{
		if (this is T)
		{
			var eventField = GetType()
				.GetField(eventName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			var eventHandler = eventField
				?.GetValue(this);
			var invokeMethod = eventHandler
				?.GetType()?.GetMethod("Invoke");
			// FIXME - cache the invoke method
			invokeMethod
				?.Invoke(eventHandler, [sender, eventArgs]);
		}
	}
	#endregion Internals
}
