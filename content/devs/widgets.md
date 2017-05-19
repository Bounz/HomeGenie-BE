## Introduction

We've learned so far that automation programs, smart devices and services
are abstracted in HomeGenie as generic *modules*.
Depending on the type, a module can then be presented to the end-user in
the web interface with a different *widget*.

For example, to display a light dimmer module, the *dimmer* widget will 
be used as it will be for all modules of the same type. 

<div class="media-container">
    <img _self_="size-small" src="images/docs/widgets_dimmer_type.png" />
</div>

So, *widgets* are reusable UI components that are employed to display data
and information of a bound *module* and that may also contain some controls
(such as buttons, sliders, ...) for interacting with it. 

Technically speaking a widget is made out of two piece of code:
one is the *HTML* code for the view, which so determine how the widget will
looks like in the user interface; the other is the *Javascript* code that
will determine actions to be taken upon user interaction or when a parameter
of the bound module is updated.

We'll now see how to explore, customize and create widgets by using
the integrated *Widget Editor*.


## Widget Editor

*Widget Editor* is accessible from the **Configure &rarr; Automation** menu.
The main page lists all widgets that are currently available in the system.
To edit an existing widget simply press it from the list, while to create
a new one select the *Add widget* option from the *Action* menu located in
the bottom-right corner.

<div class="media-container">
    <img self="size-medium" src="images/docs/widgets_editor_list.png" />
</div>

Widgets are identified by a 3 parts path consisting of `brand`/`category`/`name`.
An example is `homegenie`/`generic`/`dimmer` :

- **brand:** *homegenie* **category:** *generic* **name:** *dimmer*

When editing a widget we can see two panels. One is the code editor that can
be switched from HTML to Javascript, while the other one is the preview
panel where the widget is actually displayed and that we can use to
test its functionality.

<div class="media-container" data-ui-load="app/components/gallery">
    <img self="size-medium" src="images/docs/widgets_editor_html.png">
    <img self="size-medium" src="images/docs/widgets_editor_js.png">
    <!--img self="size-medium" src="images/docs/widgets_editor_params.png"-->
</div>

To test the widget we first have to choose a module to bind to from the
select menu right above the preview.
We can also simulate change of module parameters by clicking the **<i class="material-icons">menu</i>**
button that is located next to the bound module select menu.


### The View - HTML

When designing the widget's *View* a few guide-lines have to be considered:

- never use the `id` attribute for elements that have to be referenced in
the *Javascript* code; use the `data-ui-field` attribute instead
- prefer the use of *CSS* classes provided by *HG UI*, which are [jQuery Mobile's CSS classes](https://api.jquerymobile.com/classes/)
and the ones defined by standard [HG's CSS file](https://github.com/genielabs/HomeGenie/blob/master/BaseFiles/Common/html/css/my.css#L265)
- since *HG UI* is based on [jQuery Mobile](http://jquerymobile.com/),
prefer using this framework instead of plain HTML; there are a couple of
other frameworks/plugins that can also be used next to [jQuery Mobile](http://jquerymobile.com/)
and that are listed at the end of this page

While editing the HTML code, to update the widget preview hit `CTRL+S`
keys or by pressing the **<i class="material-icons">check_circle</i>Run/Preview** button.

#### Example - HTML code for a basic widget container
```html
<!-- main widget container -->
<div data-ui-field="widget" 
     class="ui-overlay-shadow ui-corner-all ui-body-inherit hg-widget-a">
     <!-- widget content begin -->
    <h1>Simple widget with a button</h1>
    <input data-ui-field="test-btn" type="button" class="ui-btn" />
    <br/>
    Module Name: <span data-ui-field="name">...</span>
     <!-- widget content end -->
</div>
```


### The Controller - Javascript

The javascript code takes care of updating the data displayed in the
widget's view and also of sending proper commands when the user presses
buttons and other controls that might be implemented in it.

The current version of the widget *Javascript* controller is called **v2**.

`[ // TODO: explain the '$$' widget context object ]`


```javascript
/*
# Quick-Reference for v2 widgets

 "$$" is the widget class instance object

 Widget class methods and properties:

 Get the jQuery element for a "data-ui" field
   $$.field('<widget_field_name>')

 Get the jQuery element in the main document
   $$.field('<document_tree_selector>', true)

 Call HG API Web Service 
   $$.apiCall('<api_method>', function(response){ ... })

 Get the bound module object
   $$.module

 Get a parameter of the bound module
   $$.module.prop('<param_name>')
   e.g.: $$.module.prop('Status.Level')

 Invoke a module command
   $$.module.command('<api_command>', '<command_options>', function(response) { ... })
   e.g.: $$.module.command('Control.Off')

 Shorthand for HG.Ui
   $$.ui

 Shorthand for HG.WebApp.Utility
   $$.util

 Shorthand for HG.WebApp.Locales
   $$.locales

 Blink a widget field and the status led image (if present)
   $$.signalActity('<widget_field_name>') 

 For a reference of HomeGenie Javascript API see:
   https://github.com/genielabs/HomeGenie/tree/master/BaseFiles/Common/html/js/api

*/
```

The old version of widget's *Javascript* code that is called **v1**, is
implemented as a json object that is formatted as shown in the example below.

#### Example - Minimal javascript code for v1 widgets

```javascript
[{
  // use this field to assign a default image to the widget
  IconImage: 'pages/control/widgets/homegenie/generic/images/icons/robot.png',

  // this field is used for initializing the widget once loaded
  Initialized: false,

  // this method is called each time the module bound to this widget is updated
  // put here the code for displaying module's data
  RenderView: function (cuid, module) {
    var container = $(cuid);
    var widget = container.find('[data-ui-field=widget]');
    var button = widget.find('[data-ui-field=test-btn]');
    var name = widget.find('[data-ui-field=name]');
    if (!this.Initialized) {
        this.Initialized = true;
        // register widget's event handlers
        button.on('click', ButtonClicked);
    }
    name.html(module.Name);
  },

  ButtonClicked: function() {
    // handler for the button click event here
    // this will make an API request in most cases
    // using jQuery
    $.get('/api/'+module.Domain+'/'+module.Address+'/Control.On', function(res){
        // request completed....
    });
    // or alternatively use HG Javascript API
    // (see the table below for more HG Javascript API examples)
    var ctrl = HG.Control.Modules;
    ctrl.ApiCall(module.Domain, module.Address, 'Control.On', '', function(res){
        // request completed...
    });
  }
}]
```

The only mandatory fields in the Javascript code of a *v1* widget are
*IconImage* and *RenderView*:

- **IconImage** is the image used to identify the widget in the *UI*. See
[List of HG UI icons](https://github.com/genielabs/HomeGenie/tree/master/BaseFiles/Common/html/pages/control/widgets/homegenie/generic/images).
- **RenderView** is a function that *HG UI* will call everytime the
bound module is updated, passing to it the parameter `cuid`, which is the
**id** attribute of the widget's container and the `module` parameter,
which is a reference to the bound module.<br/>
The `module` object has the following property fields: `Domain`, `Address`,
`Name`, `Description`, `Properties`.

As shown in the *ButtonClicked* handler, in most cases, when the user
click a widget control, an API request is made. The end-point of the
request will be usually an automation program that is [listening](programs.html#commands)
to API calls for that module domain.

Prefer using **v2** implementation since the **v1** implementation might
be deprecated at some point. 

Both *v1* and *v2* widgets can use *HG Javascript API*.

#### HG Javascript API - Common functions

```javascript
// use the "Utility" namespace
var utils = HG.WebApp.Utility;

// get a reference to a module by a given <domain> and <address>
var mod = utils.GetModuleByDomainAddress(domain, address);

// get a module parameter by name
var level = utils.GetModulePropertyByName(mod, 'Status.Level');
console.log('Module name = ' + mod.Name + ' Status.Level = ' + level.Value);

// show a confirmation request popup
utils.ConfirmPopup('Delete item', 'Are you sure?', function(confirmed) {
    if (confirmed) {
        // the action was confirmed...
    } else {
        // action canceled
    }
});

// format a date 
var today = utils.FormatDate(new Date());
// format a date with time
var todayTime = utils.FormatDateTime(new Date());

// use the "Control" namespace
var control = HG.Control.Modules;

// call HG API function
control.ApiCall(domain, address, command, options, function(response){
    // handle response here...
});

// use the "Programs" namespace
var progs = HG.Automation.Programs;

// Run a program
progs.Run(programId, options, fuction(response){
    // handle response here...
});
```

See [HG Javascript API on github](https://github.com/genielabs/HomeGenie/tree/master/BaseFiles/Common/html/js/api) for a complete
list of available namespaces and commands.

## Frameworks and Plugins

The following is a list of framework/plugins that can be used in a widget.

### Base Frameworks
- [jQuery](https://jquery.com/)
- [jQuery Mobile](http://jquerymobile.com/)

### UI Controls
- [ColorWheel](http://jweir.github.io/colorwheel/)
- [jQuery Knob](http://anthonyterrien.com/knob/)
- [jQM Datebox](http://dev.jtsage.com/jQM-DateBox/)

### Notification / Tooltip
- [qTip](http://qtip2.com/)
- [jQuery UI Notify Widget](http://www.erichynds.com/examples/jquery-notify/)

### Graphics / Custom controls
- [Flot](http://www.flotcharts.org/)
- [RaphaelJs](http://raphaeljs.com/)

### Utility
- [Moment.js](http://momentjs.com/)
- [jStorage](http://www.jstorage.info/)
