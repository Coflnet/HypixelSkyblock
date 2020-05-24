using System;
using System.IO;
using System.Linq;
using Coflnet;
using ConcurrentCollections;
using hypixel;

public class Migrator {
    public static void Migrate () {
        try {
            ItemnamesToIds ();
        } catch (Exception e) {
            Console.WriteLine ($"failed to migrate {e.Message} \n {e.StackTrace}");
            ItemPrices.Instance.Save ();
            StorageManager.Save ().Wait ();
        }
    }

    private static void ItemnamesToIds () {
        var deleteAfterRead = true;
        var path = "sitems";
        var noDetails = false;

        foreach (var item in FileController.DirectoriesNames ("*", path)) {
            var name = Path.GetFileName (item);
            if (name == ItemDetails.Instance.GetIdForName (name) && name == name.ToUpper ()) {
                // skip folders that have valid id
                continue;
            }
            noDetails = false;
            if (ItemDetails.Instance.GetIdForName (name) == name) {
                // we don't have details for this item yet :O
                noDetails = true;
                Console.WriteLine ($"Unkown {name}");
                continue;
            }
            foreach (var filePath in Directory.GetFiles (item)) {

                if (FileController.Exists (filePath)) {
                    var indexes = FileController.LoadAs<ConcurrentHashSet<ItemIndexElement>> (filePath);

                    foreach (var element in indexes) {
                        ItemPrices.Instance.AddIndex (element, name);
                    }
                    var newName = ItemPrices.Instance.PathTo (name, indexes.Last ().End);
                    Console.Write ($"\rDoing {filePath} to {newName}");
                }
                ItemPrices.Instance.Save ();
                if (deleteAfterRead) {
                    FileController.Delete (filePath);
                }
            }
            Console.WriteLine();
            
            if (deleteAfterRead && !noDetails) {
                Directory.Delete (item);
            }
        }
    }
}