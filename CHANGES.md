# v1.2.4
After putting out the patch yesterday, I decided to think for more than 12 seconds and realized I overengineered a solution when one was staring me in the face.

**If you used the previous patch, keep in mind that you will need to manually delete the extra parameters it created after reapplying your preset.**

## Fixes
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