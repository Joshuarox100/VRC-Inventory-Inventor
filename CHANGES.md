# v1.2.5
This is a quick patch to the updater so that updates between Unity 2018.4 and Unity 2019.4 for Inventory Inventor are distinguishable. Unfortunately, I'm both not knowledgeable or determined enough to maintain two separate builds of the tool for 2018.4 and 2019.4, so consider this to be the last version supported for Unity 2018.4. 

>**VERY IMPORTANT NOTE: Before upgrading your project to Unity 2019, install Inventory Inventor v1.3.0, which should release at the same time the update for VRChat goes live. Otherwise, *things will break* and you will likely be unable to upload avatars due to compiler issues until you manually update it, since the included updater likely won't work either.**

## Fixes
### v1.2.5
- Modified the VERSION file syntax to include the supported Unity version and modified the Updater to account for the information.
- Fixed an issue with the AutoUpdater that could result in it failing midway through the installation.
### v1.2.4
- Modified the solution introduced by the previous patch to not require the usage of extra unsynced parameters within the animator.
### v1.2.3
- Worked around the saved parameter issue to display the default state of the inventory to remote clients until the local client has loaded their avatar themselves.
### v1.2.2
- Added several null checks to fix problems when viewing presets for an avatar without Expression Parameters assigned.
- Fixed an oversight that caused the script to use the wrong animation file generated for objects.
### v1.2.1
- Added a null check for Subpage Items when they can't find the page they link to during appending.
### v1.2.0
- Added a null check for using Controls on an avatar without assigned parameters.
- Fixed an ingame syncing issue that was caused by some VRChat update regarding buttons.