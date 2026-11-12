# Active Scenario window reference

The Active Scenario window displays the status and controls for all instances in your currently running Play Mode Scenario. You can access this window by opening **Window** > **Play Mode** > **Active Scenario**, or by clicking the status window icon in the Play Mode status dropdown while in Play Mode.

This reference explains the behavior of each property and control in the Active Scenario window. The properties and controls that appear change depending on the instance type and scenario configuration.

## Window controls

|**Control**|**Description**|**Availability**|
|-|-|-|
|**Three-dot menu**|Opens a menu with additional options including **Refresh Virtual Project folder**, which clears all Editor instance library folders. This option is disabled when any Editor instance is active or launching.|Always available|

![Active Scenario window three-dot menu](../images/active-scenario-three-dot-menu.png)

## Instance information

|**Property**|**Description**|
|-|-|
|**Instance name**|The name of the instance (e.g., Player 1, Player 2, Server).|
|**Status**|The current state of the instance (Inactive, Launching, Active, Error).|
|**Tags**|Displays the tags assigned to this instance in the Play Mode Scenarios window.|

## Instance controls

### Editor instances

|**Control**|**Description**|**Availability**|
|-|-|-|
|**Activate/Deactivate button**|Manually activate or deactivate this Editor instance.|Only when a scenario is not actively launching.|
|**Keep Active checkbox**|When checked, keeps the Editor instance process alive even after the scenario or Play mode ends. When unchecked, the instance deactivates automatically when the scenario ends.|Always available for Editor instances.|
|**Focus on Player icon** ![Focus icon](../images/go-to-dashboard-32.png)|Brings the Editor instance window to focus.|Only when the instance is active.|

### Local instances

|**Control**|**Description**|**Availability**|
|-|-|-|
|**Run button**|Launches this local instance independently when in Manual control mode.|Only when running mode is set to Manual control.|
|**Running mode toggle**|Switch between **Scenario control** (![linked icon](../images/Linked@2x.png)) and **Manual control** (![unlinked icon](../images/UnLinked@2x.png)). Manual control allows the instance to run independently from the main scenario, creating a free running instance.|Always available for local instances.|

### Status indicators

|**Indicator**|**Description**|
|-|-|
|**Coherence drift warning**|Appears when a manually controlled instance is out of sync with the main Editor due to code, scene, or asset changes. Restart the instance to resolve the drift.|
|**Error indicator**|Displays error information when an instance fails to launch or encounters a critical issue.|

## Additional resources
* [Editor instances](../instance-types/main-and-additional-editor-instances.md)
* [Local instances](../instance-types/local-instance.md)
* [Scenario configurations](../play-mode-scenario/play-mode-scenario-configurations.md)
* [Create a Play Mode scenario](../play-mode-scenario/play-mode-scenario-create.md)
