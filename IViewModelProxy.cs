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

namespace BruceMellows.MVVM.ViewModel.Proxy;

public interface IViewModelProxy<TViewModel> : IPropertyAndEventProxy<TViewModel> where TViewModel : class
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
}
