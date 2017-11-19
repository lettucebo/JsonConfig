README
=====================

## About
JsonConfig.Core is a simple to use configuration library, allowing JSON based config files for your C#/.NET or .NET Core application instead of cumbersome web.config/app.config xml files.

It is based on JSON.Net and C# 4.0 dynamic feature. Allows putting your programs config file into .json files, where a default config can be embedded as a resource or put in the (web-)application folder. Configuration can be accessed via dynamic types, no custom classes or any other stub code is necessary.

JsonConfig brings support for *config inheritance*, meaning a set of configuration files can be used to have a single, scoped configuration at runtime which is a merged version of all provided configuration files.

## Example

Since my lack of skills in writing good examples into a documentation file, it is best to take a look at the [examples](https://github.com/lettucebo/JsonConfig.Core/tree/master/JsonConfig.Core.Example) with a complete commented which will give you a better understanding.

### Getting started

Usually the developer wants a default configuration that is used when no configuration by the user is present whatsoever. Often, this configuration is just hardcoded default values within the code. With JsonConfig.Core there is no need for hardcoding, we simply create a default.conf file and embedd it as a resource.

Let's create a sample default.conf for a hypothetical grocery store:

```
# Lines beginning with # are skipped when the JSON is parsed, so we can
# put comments into our JSON configuration files
{
	StoreOwner : "Money Yu",

	# List of items that we sell
	Fruits: [ "alpha", "bravo", "charle" ]
}
```

JsonConfig automatically scan's all assemblies for the presence of a `default.json` file, so we do not have to add any boilerplate code and can directly dive in:

```csharp
// exmaple code using our configuration file
using JsonConfig.Core;
[...]
public void PrintInfo () 
{
	var storeOwner = Config.Default.StoreOwner;

	Console.WriteLine ("Hi there, my name is {0}!", storeOwner);

	foreach (var fruit in Config.Default.Fruits)
		Console.WriteLine (fruit);
}
```
However, the developer wants the user to make his own configuration file. JsonConfig.Core automatically scans for a json files in the `App_Data` folder path of the application.

```
# sample settings.conf
{
	Fruits: [ "melon", "peach" ]	
}
```

The `App_Data folder json files` and the `default.json` are then merged in a clever way and provided via the *Global* configuration.

```csharp
public void PrintInfo () 
{
	// will result in apple, banana, pear 
	foreach (var fruit in Config.Default.Fruits)
		Console.WriteLine (fruit);

	// will result in melon, peach
	foreach (var fruit in Config.User.Fruits)
		Console.WriteLine (fruit);

	// access the Global scope, which is a merge of Default
	// and User configuration
	// will result in apple, banana, pear, melon, peach
	foreach (var fruit in Config.Global.Fruits)
		Console.WriteLine (fruit);
}
```

### Nesting objects

We are not bound to any hierarchies, any valid JSON is a valid configuration
object. Take for example a hypothetical webserver configuration:

```
{
	ListenPorts: [ 80, 443 ],
	EnableCaching : true,
	ServerProgramName: "Hypothetical WebServer 1.0",

	Websites: [
		{
			Path: "/srv/www/example/",
			Domain: "example.com",
			Contact: "admin@example.com"	
		},
		{
			Path: "/srv/www/somedomain/",
			Domain: "somedomain.com",
			Contact: "admin@somedomain.com"
		}
	]
}
```

Above configuration could be accessed via:

```csharp
using JsonConfig.Core;
[...]

public void StartWebserver () 
{
	// access via Config.Global
	string serverName = Config.Global.ServerProgramName;
	bool caching = Config.Global.EnableCaching;
	int[] listenPorts = Config.Global.ListenPorts;

	foreach (dynamic website in Config.Global.Websites) 
	{
		StartNewVhost (website.Path, website.Domain, website.Contact);
	}
}
```
