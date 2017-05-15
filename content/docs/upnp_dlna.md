<div class="content-margin" align="center">
    <iframe self="size-medium" height="440" src="https://www.youtube.com/embed/ygilmTwLSJ0?rel=0" frameborder="0" allowfullscreen></iframe>
</div>

## Controlling media servers and players

HomeGenie can be used as a control point for DLNA/UPnP devices such as:

- Routers / Media Servers
- Smart TVs / Media Players
- UPnP switches and lights

Media servers and players in the network are automatically detected
and can be added like other modules to *[control groups](#/docs/configure)*.

From there we can browse media files (pictures, music and videos) and play
them to any media player/renderer in the local network.
This can be done either from the web interface or the mobile client.

<div class="media-container">
    <img self="size-medium" title="Web UI - Media Server Widget" src="images/docs/media_server_widget_00.png">
</div>

DLNA/UPnP commands can also be stored in a script with *[Record Macro](#/docs/scenarios)*
functionality or manually by using *[UPnP API](./api/mig/mig_api_upnp.html)* 
and then recalled within a *[scenario](#/docs/scenarios)*.
