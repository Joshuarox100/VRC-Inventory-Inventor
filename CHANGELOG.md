# Inventory Inventor v1.4.0 (VPM Support)
Back from the dead! Except I never was... I just didn't have the drive to work on this again for a while. *Annnnnnnnyway...*

So, if you haven't heard, VRChat has recently released a new piece of software called the VRChat Creator Companion (VCC). At the time of writing, it is currently in beta, but from my experience, the software is stable enough *and* useful enough that I've decided to fully integrate II with it pre-emptively.

Does this mean that you're required to use VCC if you wish to use II going forward? Well... no. I know that most people will want to keep using II and not use the VCC for their projects for a decent while, especially considering that it will be in beta for several more months at least. 

For this reason v1.3.x will remain avaiable as an LTS version for non-VPM projects and will be maintained as the primary branch until either the VCC leaves beta or non-VPM SDKs are deprecated. At that time, it will be archived and replaced by the VCC branch.

This and future feature updates will be offered exclusively as VCC packages, but you know how slow I am with those so it's not like that'll be too relevant really.

## New Features
- **VPM Support!**
	Full support has been added for the VCC's VPM and, going forward, this will be the primary way to receive updates to Inventory Inventor going forward.

## Changes
- **General**
	- The entire tool has been refactored and restructured to be a valid VPM package.
	- Examples are now an optional import which you can perform through Unity's own package manager.
- **Settings**
	- Settings are now saved as a JSON file in the ProjectSettings folder so they are not able to be replaced accidentally during updates.