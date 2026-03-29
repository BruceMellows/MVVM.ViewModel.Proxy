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

namespace BruceMellows.MVVM.ViewModel.Proxy;

public abstract class ViewModelProxy<TViewModel> : IViewModelProxy<TViewModel>, INotifyPropertyChanging, INotifyPropertyChanged
	where TViewModel : class
{
	public event PropertyChangingEventHandler? PropertyChanging;
	public event PropertyChangedEventHandler? PropertyChanged;

	#region fields
	private readonly TViewModel? _viewModelProxy;
	private readonly Dictionary<string, object?> _properties = [];
	private readonly ConcurrentDictionary<string, IPropertyBinding> bindings = [];
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
		if (!_properties.TryGetValue(key, out var value))
		{
			_properties[key] = value = getDefaultValue();
		}

		return value;
	}

	public bool SetValue(string key, object? value)
	{
		PropertyChanging?.Invoke(ViewModel, new PropertyChangingEventArgs(key));
		if (_properties.TryGetValue(key, out var oldValue) && Equals(oldValue, value))
		{
			return false;
		}

		_properties[key] = value;
		PropertyChanged?.Invoke(ViewModel, new PropertyChangedEventArgs(key));
		if (bindings.TryGetValue(key, out var binding))
		{
			binding.Invoke(ViewModel, value!);
		}

		return true;
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

	#endregion Internals
}
