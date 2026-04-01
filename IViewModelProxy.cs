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
using System.ComponentModel;

namespace BruceMellows.MVVM.ViewModel.Proxy;

public interface IViewModelProxy<TViewModel> where TViewModel : class
{
	/// <summary>
	/// This is called when a property change is begun, irrespective of whether the property value is actually changed or not.<br/>
	/// This can be used to perform some actions before the property value is changed, such as validation or logging.
	/// </summary>
	/// <param name="key"></param>
	/// <param name="viewModel"></param>
	void OnPropertyChangeBegin(string key, TViewModel viewModel);

	/// <summary>
	/// This is called if a property change is actually made, immediately after the change has been made.
	/// </summary>
	/// <param name="key"></param>
	/// <param name="viewModel"></param>
	void OnPropertyChanged(string key, TViewModel viewModel);

	/// <summary>
	/// This is called when a property change has ended, irrespective of whether the property value is actually changed or not.<br/>
	/// This can be used to perform some actions after the property value is changed, such as updating the UI or triggering other changes.
	/// </summary>
	/// <param name="key"></param>
	/// <param name="viewModel"></param>
	void OnPropertyChangeEnd(string key, TViewModel viewModel);

	/// <summary>
	/// Return the (nullable) value for the supplied key.<br/>
	/// If the key is not found, the getDefaultValue function is called to get a default value, which is then stored and returned.<br/>
	/// This allows for lazy initialization of properties.
	/// </summary>
	/// <param name="key"></param>
	/// <param name="getDefaultValue"></param>
	/// <returns></returns>
	object? GetValue(string key, Func<object> getDefaultValue);

	/// <summary>
	/// Set the value for the supplied key.<br/>
	/// If the supplied value is not the same as the existing value, the PropertyChanged method is called.<br/>
	/// The PropertyChangeBegin and PropertyChangeEnd methods are called regardless of whether the value is actually changed or not, allowing for actions to be performed before and after the change.<br/>
	/// </summary>
	bool SetValue(string key, object? value);

	/// <summary>
	/// Get the view model instance that this proxy represents. The view model is created by the proxy and implements the interface TViewModel.
	/// </summary>
	TViewModel ViewModel { get; }
}
