# TrayAppUtility
Provides high-level APIs to create task-executing tray applications in Window Presentation Foundation (WPF)

### Features
- Progress API
  - Ability to set total and processed items
  - Getter for estimated time to completion
- Logging API
  - Each task generates a separate log file
  - All tasks are automatically measured for time. Runtime is appended to the log file.
  - Log files older than a month are automatically deleted
  - Time-intensive action parts can be measured manually
  - An attribute to disable logging for particular action
- Action
  - Declared as a public static method
  - Ability to set tray action that will be used to populate tray icon context menu
  - Ability to set default tray action that will be executed on double click if the tray is not in the error state
  - Receives an instance of log writer
  - Can be canceled at any time by the user
  - Ability to periodically execute action automatically
- Tray
  - Custom tray icon
  - Context menu populated with users' actions
  - Double-clicking executes default action if specified
  - When an action is being executed that uses Progress API, a radial progress bar is overlayed over the tray icon
  - When an action is being executed a tooltip is set containing
    - Action name
    - Processed and total item counts as well as percentage completed
    - Estimated time to completion
    - Last log entry written
  - When an action throws an unhandled exception tray app enters an error state
    - Progress bar overlay changes color to red
    - Double-clicking the tray icon opens the last log file and clears the error state
    - Any context menu action clears the error state and starts their respective action
- Utilities
    - Nice name method
    - Open file or folder with default application (can wait for close)
    - Method to show toast notifications with click action

### Tutorial

#### Configuring tray app utility
In order to start using the tray app utility we need create a new project from WPF application template and set its window as the startup window. We can do this in `App.xaml` file

```xaml
<Application x:Class="Tutorial.App"
   xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
   StartupUri="pack://application:,,,/TrayAppUtility;component/TrayApp.xaml"> <!-- Set tray app as startup window-->
</Application>
```

Without any further actions, the application should not generate an empty tray app with a context menu containing default utility actions.

![BFjcNzG8kp](https://github.com/Planktomas/TrayAppUtility/assets/94010480/1b12476c-37bc-48c1-9169-a75c83d24b0f)

#### Changing tray icon
Using the tray app utility is quite simple. One of the first things we might want to do is set our custom icon. This can be done by adding a png image with the name "TrayIcon.png" to the project files.

![devenv_1XKmYNLobO](https://github.com/Planktomas/TrayAppUtility/assets/94010480/85371a29-36b9-4305-acb0-603bcef0bc37)

Then you need to make the PNG file build action to be set to "Embedded resource".

![devenv_6D8WrFX9iX](https://github.com/Planktomas/TrayAppUtility/assets/94010480/f7439bf3-a86c-4a8b-a856-80a65af8c859)

This will make your custom icon discoverable by tray app utility. Once the project is built, you should see tray app use your icon.

![yYaggdnX8h](https://github.com/Planktomas/TrayAppUtility/assets/94010480/ac686bb0-3f0e-475c-822a-ffe4117aa07a)

#### Defining an action
In order to start adding your own actions to the tray app, declare a public static method. The name of the method will be used as the name of the action. Here is an example of the full method signature:
```cs
[TrayAction]
public static void Action(CancellationTokenSource cancel)
{
}
```

Of course, there is no point in leaving an empty action so let's fill it with some work to do:
```cs
[TrayAction]
public static void Action(CancellationTokenSource cancel)
{
    var length = 100;
    Progress.Total = length;

    for (int i = 0; i < length; i++)
    {
        if (cancel.IsCancellationRequested)
        {
            Log.Write($"Cancelling Default Action");
            return;
        }

        Thread.Sleep(100);
        Progress.Increment($"Processed item {i}");
    }
}
```

This is a fully implemented tray action. That's how it should look in action:

![oeZLAbwDB0](https://github.com/Planktomas/TrayAppUtility/assets/94010480/8fc88b2d-3910-4d0d-b910-1b43784cca55)

#### Defining a default action
Default actions are created identically as tray actions but using a different attribute. Here is an example of making a default action:
```cs
[TrayDefault]
public static void DefaultAction(CancellationTokenSource cancel)
{
...
}
```

Now tray app will execute this action upon double click.

![rYYocKeURP](https://github.com/Planktomas/TrayAppUtility/assets/94010480/12add970-f7b7-4591-885e-cafce8200d3a)

#### No logging
Some actions are so simple that they don't need logs. You can disable logging for a particular action using NoLog attribute:
```cs
[TrayAction]
[TrayDefault]
[NoLog]
public static void DefaultAction(CancellationTokenSource cancel)
{
...
}
```

You will still see log messages in tray tooltips but they won't be saved into a dedicated log file.

#### Automatic action execution
We may want to execute actions automatically at a specified interval. This can be achieved using Autorun attribute. The attribute accepts a TimeSpan string in format Hours:Minutes:Seconds.Milliseconds. Here is how it looks in use:
```cs
[TrayAction]
[Autorun("00:00:30")]
public static void Notification(CancellationTokenSource cancel)
{
    TrayUtils.ShowNotification("Notification", "Message text",
        () => Log.Write("Log entry from notification"));
}
```

Now every 30 seconds you'll see this toast notification:

![NAi1raRU7h](https://github.com/Planktomas/TrayAppUtility/assets/94010480/3202c028-3cca-4fdc-8e2b-7a1815ef9f9d)

[A complete tutorial app can be found here](https://github.com/Planktomas/TrayAppUtility/tree/main/Tutorial)
