// Flushes Emscripten's in-memory filesystem (persistentDataPath) to IndexedDB so WebGL saves survive a reload.
mergeInto(LibraryManager.library, {
  IG_SyncFs: function () {
    FS.syncfs(false, function (err) { if (err) console.warn("[SaveService] IndexedDB sync failed:", err); });
  }
});
