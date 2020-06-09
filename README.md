# PingPlugin
A ping display plugin for Dalamud.

This plugin provides a ping monitor and graph that show your latest ping and your average ping over a configurable number of steps.

**This plugin does not work, and likely will not ever work, with Mudfish FastConnect. This is because FastConnect works by sending acknowledgements back to the client immediately after a message is sent, which is how the round-trip-time (RTT) is calculated. The acknowledgement time is almost immediate, and so the ping readout will always be 0ms.**

## Screenshots
![Screenshot](https://raw.githubusercontent.com/karashiiro/PingPlugin/master/Assets/1.png)
![Screenshot](https://raw.githubusercontent.com/karashiiro/PingPlugin/master/Assets/2.png)

## Usage
* `/ping` - Show/hide the ping monitor.
* `/pinggraph` - Show/hide the ping graph.
* `/pingconfig` - Open the configuration from chat.

## Configuration
![Screenshot](https://raw.githubusercontent.com/karashiiro/PingPlugin/master/Assets/0.png)
* Lock plugin windows: This prevents you from accidentally dragging the windows around, but still allows you to hover over the graph and collapse it.
* Click through plugin windows: This ignores plugin window clicks, instead treating them as game clicks.
* Hide overlays during cutscenes: It's what it says on the tin.
* Minimal display: This turns the monitor from a 3-line display into a 1-line display, omitting the current server's IP address.
* Hide errors: If you like, this prevents errors from being displayed, which expand the monitor by one line temporarily.
Though it's not recommended to hide errors, most errors are just one-time server timeouts, and can be safely ignored.
* Recorded pings: This is the number of pings that are displayed in the graph and averaged over. Increasing this will increase the computational load of the plugin negligibly.
* Monitor Color: The color of the monitor text.
* Error Color: The color of the error text.
* Monitor Opacity: How see-through the monitor background is.
* Language: Current options include English, Japanese, Spanish, and German. Feel free to PR additional translations!
