# MonitorBrightnessAutoAdjust
auto adjust all minitors brightness by ambient light. Use TSL2591 light sensor and  CH341T usb to I2C converter adapter

## Hardware

- TSL2591 I2C light sensor
- CH341T USB to I2C converter adapter

## Publish

```bat
dotnet publish -c Release 
```

## Windows auto startup

Right click notify icon, check AutoStart

## Screen shot
![AmbientLight: 156Lux](images\\screenshots_1.png)
![AmbientLight: 14Lux](images\\screenshots_2.png)
![AmbientLight: 0Lux](images\\screenshots_3.png)

## LightSensor placement

![LightSensor placement](images\\lightSensor_placement.png)

## Interpolated ALR Curve

![Interpolated ALR Curve](images\\interpolated_ALR_Curve.png)