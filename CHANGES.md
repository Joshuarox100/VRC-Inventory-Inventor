# v1.3.0
WOOT UNITY 2019 IS HERE BABY! DARK MODE FOR ALL! *cough*- sorry about that. *Anyways*, this is a **feature update**! Several new features have been added, as well as support and compatibility for Unity 2019.

Unfortunately, due to a change in location for some class references needed for Inventory Inventor, from here on out, it will only be compatible with Unity 2019. If you plan on staying with Unity 2018, I recommend you use v1.2.5 instead (or just migrate, silly). 

Due to the somewhat sudden release of VRChat's Unity 2019 upgrade, this version hasn't been able to be vigorously tested in the updated engine. **Visual bugs may be present, if you find one, please report it to the Issue's page on Github, not to me on Discord.**

Additionally, ***if you are currently on v1.2.4, the Auto Updater will not work. You will need to import this update manually.*** If you are on v1.2.5, you should be able to update **before migrating**. There will be compiler errors after the import, but they should go away once you load the project again in Unity 2019.

## Changes
- Added support for Unity 2019:
	- All UI elements should now be compatible with both light and dark theme.
	- The icon for Presets has been updated to match the new design language of the Editor.
- Copying & pasting is now supported for Pages, Items, & Groups.
- You can now duplicate or delete Pages & Items after right-clicking them in the Directory.
- You can now set all members of a Group at once or clear a Group entirely after right-clicking the Group header.
- When using Animation Clips in Toggles, you can now set a duration for the transition in either fixed or normalized time.
- When using Animation Clips in Toggles, you can now offset the animation's starting point while your avatar is loading, this is useful for progressive animations such as dissolves.
- Toggles with syncing disabled (local-only) are now able to be saved locally.
- The location of relevant windows such as the Manager have been relocated to "Tools/Joshuarox100" instead of "Tools/Avatars 3.0"
- The last used destination is now saved within each Preset and is automatically used if possible when selecting the Preset in the Manager.
- The README has been updated with new documentation for introduced features and includes some modifications to the Troubleshooting section.
- A new social preview for the Github has been applied and is also included in the "Images" folder.

## Fixes
### v1.3.0
- **All** instances of the chosen Animator are now replaced in the Avatar Descriptor.
- Fixed a long-standing bug with new Page creation that occurred when names were already in use (The correct item should now be renamed).
- Aaaaaand here's all the Unity 2019 related ones:
	- Changed references for "UnityEngine.Experimental.UIElements" to "UnityEngine.UIElements" so it would compile.
	- Removed the IsDirtyUtility Subclass since the "EditorUtility.IsDirty" function is Public in Unity 2019.
	- Added a bar to the tops of Reorderable list footers since they no longer have one by default.
	- Fixed Data & Memory usage bar colors so that they work in both light & dark mode.
	- Fixed the styling of ToolbarTextFields to not blend in with the color of Box sections.
	- Changed the style of overlapping Boxes to Helpboxes since the border of Boxes was removed.
	- Adjusted some spacing between elements in the Manager window to prevent the Create button from going off the screen.
	- Subtly changed the way the destination option was displayed in the manager to better align with other settings.
	- Adjusted the About window to account for the new VERSION file format.
	- Edited the text in the About window as well as several Textfields & Labels scattered in other areas to support dark theme.
	- Edited the "Append Preset" window to both support dark theme and to not crunch the "No Preset Selected" text.
	- Moved a SetDirty flag about 8 lines higher to stop a weird bug where menus set as submenus would be removed from the exported assets occasionally.