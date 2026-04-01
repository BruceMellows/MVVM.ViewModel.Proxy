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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public abstract class ViewModelProxy<TViewModel> : PropertyAndEventProxy<TViewModel>, IViewModelProxy<TViewModel>
	where TViewModel : class
{
	public ViewModelProxy()
	{
	}

	public override void OnPropertyChangeBegin(string key, TViewModel viewModel)
	{
		RaiseEvent<INotifyPropertyChanging>(nameof(INotifyPropertyChanging.PropertyChanging), Proxied, new PropertyChangingEventArgs(key));
	}

	public override void OnPropertyChanged(string key, TViewModel viewModel)
	{
		RaiseEvent<INotifyPropertyChanged>(nameof(INotifyPropertyChanged.PropertyChanged), Proxied, new PropertyChangedEventArgs(key));
	}

	public override void OnPropertyChangeEnd(string key, TViewModel viewModel)
	{
	}


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
}
