﻿using System;
using System.Linq;
using Timer = System.Windows.Forms.Timer;

namespace Monitorian.Core.Models.Watcher;

internal abstract class TimerWatcher : IDisposable
{
	private readonly Timer _timer;
	private readonly TimeSpan[] _intervals;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="intervals">Sequence of timer intervals in seconds</param>
	protected TimerWatcher(params int[] intervals)
	{
		if (intervals is not { Length: > 0 })
			throw new ArgumentNullException(nameof(intervals));
		if (intervals.Any(x => x <= 0))
			throw new ArgumentOutOfRangeException(nameof(intervals), intervals.First(x => x <= 0), "An interval must be positive.");

		this._intervals = intervals.Select(x => TimeSpan.FromSeconds(x)).ToArray();

		_timer = new Timer();
		_timer.Tick += OnTick;
	}

	public int Count { get; private set; }

	public void TimerStart()
	{
		_timer.Stop();
		Count = 0;
		_timer.Interval = (int)_intervals[Count].TotalMilliseconds;
		_timer.Start();
	}

	public void TimerStop()
	{
		_timer.Stop();
	}

	private void OnTick(object sender, EventArgs e)
	{
		_timer.Stop();
		Count++;

		TimerTick();

		if (Count < _intervals.Length)
		{
			_timer.Interval = (int)_intervals[Count].TotalMilliseconds;
			_timer.Start();
		}
	}

	protected abstract void TimerTick();

	#region IDisposable

	private bool _isDisposed = false;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_isDisposed)
			return;

		if (disposing)
		{
			// Free any other managed objects here.
			_timer.Stop();
			_timer.Tick -= OnTick;
		}

		// Free any unmanaged objects here.
		_isDisposed = true;
	}

	#endregion
}