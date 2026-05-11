import AppKit

enum FileLoader {

    /// Converts a list of file URLs into new RenameFile objects.
    /// Deduplicates against existing files AND within the batch itself.
    /// Directories are silently skipped.
    static func makeFiles(from urls: [URL], existing: [RenameFile]) -> [RenameFile] {
        let existingURLs = Set(existing.map(\.originalURL))
        var seen = Set<URL>()
        return urls.compactMap { url in
            var isDir: ObjCBool = false
            guard FileManager.default.fileExists(atPath: url.path, isDirectory: &isDir),
                  !isDir.boolValue,
                  !existingURLs.contains(url),
                  seen.insert(url).inserted
            else { return nil }
            return RenameFile(originalURL: url)
        }
    }

    /// Returns all top-level regular files (not subdirectories, not hidden) inside a folder URL,
    /// sorted alphabetically by filename.
    static func topLevelFileURLs(in folder: URL) -> [URL] {
        let contents = (try? FileManager.default.contentsOfDirectory(
            at: folder,
            includingPropertiesForKeys: [.isRegularFileKey],
            options: [.skipsHiddenFiles]
        )) ?? []
        return contents
            .filter { url in
                (try? url.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile) == true
            }
            .sorted { $0.lastPathComponent < $1.lastPathComponent }
    }

    /// Opens an NSOpenPanel to select a folder. Calls completion with top-level file URLs or empty if cancelled.
    @MainActor
    static func openFolderPanel(completion: @escaping ([URL]) -> Void) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        panel.message = "Choose a folder to load files from"
        panel.begin { response in
            guard response == .OK, let url = panel.url else { completion([]); return }
            completion(topLevelFileURLs(in: url))
        }
    }

    /// Opens an NSOpenPanel for multi-file selection. Calls completion with chosen URLs or empty if cancelled.
    @MainActor
    static func openFilePanel(completion: @escaping ([URL]) -> Void) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = true
        panel.message = "Choose files to rename"
        panel.begin { response in
            guard response == .OK else { completion([]); return }
            completion(panel.urls)
        }
    }
}
