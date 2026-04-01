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

namespace BruceMellows.MVVM.ViewModel.Proxy;

public static class DoDoneAction
{
	public static IDisposable Create(Action onDone) => new Implementation<bool>(() => true, _ => onDone());
	public static IDisposable Create(Action onDo, Action onDone) => new Implementation<bool>(() => { onDo(); return true; }, _ => onDone());
	public static IDisposable Create<T>(Func<T> onDo, Action<T> onDone) => new Implementation<T>(onDo, onDone);

	private sealed class Implementation<T>(Func<T> onDo, Action<T> onDone) : IDisposable
	{
		readonly Action<T> onDone = onDone;
		readonly T value = onDo();
		public void Dispose() => onDone(value);
	}
}