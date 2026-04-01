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
using System.Threading;
using System.Linq.Expressions;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public abstract class PropertyAndEventProxy<TPropertyAndEventInterface> : IPropertyAndEventProxy<TPropertyAndEventInterface>
	where TPropertyAndEventInterface : class
{
	private readonly TPropertyAndEventInterface? _proxied;
	private readonly Dictionary<string, object?> _properties = [];
	private readonly ConcurrentDictionary<string, IPropertyBinding> bindings = [];
	private readonly ThreadLocal<HashSet<string>> _currentBindings = new(() => []);

	public PropertyAndEventProxy()
	{
		_proxied = PropertyAndEventProxyFactory.CreateProxy(this);
	}

	protected void OnChanged<TResult>(Expression<Func<TPropertyAndEventInterface, TResult>> expression, Action<TPropertyAndEventInterface, TResult> callback)
	{
		var propertyName = (expression.Body as MemberExpression)?.Member.Name
			?? throw new ArgumentException("Expression must be a member expression.", nameof(expression));
		bindings[propertyName] = new PropertyBinding<TResult>(callback);
	}

	public TPropertyAndEventInterface Proxied => _proxied!;

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
			() => OnPropertyChangeBegin(key, Proxied),
			() => OnPropertyChangeEnd(key, Proxied));

		if (_properties.TryGetValue(key, out var oldValue) && Equals(oldValue, value))
		{
			return false;
		}

		_properties[key] = value;
		OnPropertyChanged(key, Proxied);

		if (bindings.TryGetValue(key, out var binding))
		{
			binding.Invoke(Proxied, value!);
		}

		return true;
	}

	public virtual void OnPropertyChangeBegin(string key, TPropertyAndEventInterface viewModel)
	{
	}

	public virtual void OnPropertyChanged(string key, TPropertyAndEventInterface viewModel)
	{
	}

	public virtual void OnPropertyChangeEnd(string key, TPropertyAndEventInterface viewModel)
	{
	}

	private interface IPropertyBinding
	{
		void Invoke(TPropertyAndEventInterface viewModel, object value);
	}

	private class PropertyBinding<TResult>(Action<TPropertyAndEventInterface, TResult> callback) : IPropertyBinding
	{
		private Action<TPropertyAndEventInterface, TResult> callback = callback;
		public void Invoke(TPropertyAndEventInterface viewModel, object? value) => callback(viewModel, (TResult)value!);
	}
}
