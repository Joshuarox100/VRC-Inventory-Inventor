# Inventory Inventor

Author: Joshuarox100

Description: Make inventories fast with Inventory Inventor! With it, you can create inventories with up to 64 synced toggles, all contained within a single Expression Parameter!

Dependencies: 
- [BMB Libraries](https://github.com/Joshuarox100/BMB-Libraries) (Included)
- VRCSDK3-AVATAR (Not Included)

## Installation Guide
Simply download and import the latest **Unity Package** from [**Releases**](https://github.com/Joshuarox100/VRC-Inventory-Inventor/releases) on GitHub **(You will have issues if you don't)**.

<p align="center">
  <img width="80%" height="80%" src="Images/Other/Installation.png">
</p>

## How to Use
1) To create an Inventory, first create an Inventory Preset as described in [Creating a Preset](#creating-a-preset).

<p align="center">
  <img width="80%" height="80%" src="Images/How To Use/Step 1.png">
</p>

2) To apply the preset to your Avatar, open the manager located under 'Tools -> Avatars 3.0 -> Inventory Inventor -> Manage Inventory'.

<p align="center">
  <img width="80%" height="80%" src="Images/How To Use/Step 2.png">
</p>

3) Configure the manager with your preset as described in [Using the Manager](#using-the-manager).

<p align="center">
  <img width="80%" height="80%" src="Images/How To Use/Step 3.png">
</p>

4) Finally, click Create to apply the preset to your Avatar! 

If you have any issues or questions, look in the [troubleshooting](#troubleshooting) and [questions](#common-questions) sections below before [contacting me](#contacting-me).

## Creating a Preset
To add an Inventory to an Avatar, you first need to create a Preset!

To create a new Preset, right click the Asset browser and select 'Create -> Inventory Inventor -> Preset'.

<p align="center">
  <img width="80%" height="80%" src="Images/Preset/Create.png">
</p>

After you name the newly created Preset, you should see something similar to the below image.

<p align="center">
  <img width="80%" height="80%" src="Images/Preset/Default.png">
</p>

From here, it gets a lot more open ended. If you would like to jump to a particular topic, use the below links.

1. [Pages](#pages)
2. [Items](#items)
3. [Groups](#groups)
4. [Hints](#hints)

### Pages
Default

### Items
Default

### Groups
Default

### Hints
Default

## Using the Manager


## Common Questions
**Can I make submenus using the UI?**
>Not yet! That feature is planned for the future though.

**Can I have multiple inventories on a single avatar?**
>Let me answer your question with a another question: *Why do you need more than 64 toggles to begin with?*  
	Truthfully though, if you are seriously needing that much inventory space, perhaps you should consider splitting it up into multiple avatars for performance reasons alone or consider other ways to achieve what you're attempting to do. That said, once I do implement submenu creation in my UI, I will be raising the limit to 85 items to accomodate it.

**How do those who join the world late see me?**
>If you leave Auto Sync on, the current state of your inventory will be synced to them over a short period of time while the system is idle. If Auto Sync is left off, late-joiners will only see the initial state of the objects until you toggle them again, a bit like how toggles work in Avatars 2.0.

## Troubleshooting
**My Inventory isn't syncing correctly to people joining late.**
>Your Refresh Rate may be too fast for the network to handle. Try recreating your inventory using a slower time.

**The Debug menu is just showing random numbers for each of the item layers.**
>This is a visual bug caused by having State Machines named differently than their originating Layer. This doesn't actually cause any problems remotely or locally so you don't need to worry about it too much. A bug report for it exists on the Feedback forum if you want to upvote it [here](https://feedback.vrchat.com/avatar-30/p/bug-debug-menu-fails-to-show-state-names-when-the-state-machine-is-named-differe).

**"An exception occured!"**
>If this happens, ensure you have a clean install of Inventory Inventor, and if the problem persists, [let me know](#contacting-me)!

## Contacting Me
If you still have some questions or recommendations you'd like to throw my way, you can ask me on Discord (Joshuarox100#5024) or leave a suggestion or issue on the [GitHub](https://github.com/Joshuarox100/VRC-Inventory-Inventor/issues) page.
