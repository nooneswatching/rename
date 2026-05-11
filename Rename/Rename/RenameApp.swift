import SwiftUI

@main
struct RenameApp: App {
    @StateObject var appState = AppState()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appState)
                .frame(minWidth: 900, minHeight: 600)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
        .commands {
            CommandGroup(replacing: .undoRedo) {
                Button("Undo Rename") {
                    appState.performUndo()
                }
                .keyboardShortcut("z", modifiers: .command)
                .disabled(!appState.canUndo)
            }
        }
    }
}
