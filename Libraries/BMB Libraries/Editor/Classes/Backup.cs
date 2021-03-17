using System;
using System.IO;
using UnityEditor.VersionControl;
using UnityEngine;

namespace InventoryInventor.BMBLibraries
{
    namespace Classes
    {
        public class Backup
        {
            private string[] assets;
            private byte[][] backup;

            public Backup()
            {
                assets = new string[0];
                backup = new byte[0][];
            }

            public Backup(AssetList assets)
            {
                this.assets = new string[assets.ToArray().Length];
                backup = new byte[this.assets.Length][];
                for (int i = 0; i < this.assets.Length; i++)
                {
                    try
                    {
                        this.assets[i] = assets[i].path;
                        backup[i] = File.ReadAllBytes(assets[i].path);
                    }
                    catch (Exception err)
                    {
                        backup[i] = null;
                        Debug.LogError(err);
                    }
                }
            }

            public void AddToBackup(Asset asset)
            {
                int index = -1;
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] == asset.path)
                    {
                        index = i;
                        break;
                    }
                }

                if (index != -1)
                {
                    try
                    {
                        backup[index] = File.ReadAllBytes(asset.path);
                    }
                    catch (Exception err)
                    {
                        backup[index] = null;
                        Debug.LogError(err);
                    }
                }
                else
                {
                    string[] newAssets = new string[assets.Length + 1];
                    assets.CopyTo(newAssets, 1);
                    newAssets[0] = asset.path;
                    assets = newAssets;

                    byte[][] newBytes = new byte[backup.Length + 1][];
                    backup.CopyTo(newBytes, 1);
                    try
                    {
                        newBytes[0] = File.ReadAllBytes(asset.path);
                    }
                    catch (Exception err)
                    {
                        newBytes[0] = null;
                        Debug.LogError(err);
                    }
                    backup = newBytes;
                }
            }

            public bool RestoreAssets()
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    try
                    {
                        if (backup[i] == null)
                        {
                            return false;
                        }
                        else
                        {
                            if (!File.Exists(assets[i]))
                            {
                                File.Create(assets[i]);
                            }
                            File.WriteAllBytes(assets[i], backup[i]);
                        }
                    }
                    catch (Exception err)
                    {
                        Debug.LogError(err);
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
