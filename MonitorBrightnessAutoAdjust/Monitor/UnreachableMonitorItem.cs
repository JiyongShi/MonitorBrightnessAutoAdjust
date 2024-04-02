﻿using System.Windows;
using Windows.Foundation;

namespace Monitorian.Core.Models.Monitor;

internal class UnreachableMonitorItem : MonitorItem
{
	public UnreachableMonitorItem(
		string deviceInstanceId,
		string description,
		byte displayIndex,
		byte monitorIndex,
		bool isInternal) : base(
			deviceInstanceId: deviceInstanceId,
			description: description,
			displayIndex: displayIndex,
			monitorIndex: monitorIndex,
			monitorRect: Rect.Empty,
			isInternal: isInternal,
			isReachable: false,
			onDisposed: null)
	{ }

	public override AccessResult UpdateBrightness(int brightness = -1) => AccessResult.Failed;
	public override AccessResult SetBrightness(int brightness) => AccessResult.Failed;
}