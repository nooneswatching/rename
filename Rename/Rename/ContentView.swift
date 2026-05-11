import SwiftUI
import UniformTypeIdentifiers

struct ContentView: View {
    @EnvironmentObject var appState: AppState
    @State private var showReviewSheet = false

    var body: some View {
        VStack(spacing: 0) {
            HSplitView {
                SidebarView()
                    .frame(minWidth: 160, maxWidth: 220)
                OrderingCanvasView()
                    .frame(minWidth: 400)
                RulesPanelView()
                    .frame(minWidth: 240, maxWidth: 320)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            PreviewBarView(onApply: { showReviewSheet = true })
                .frame(height: 48)
                .background(Color(NSColor.windowBackgroundColor))
                .overlay(Divider(), alignment: .top)
        }
        .sheet(isPresented: $showReviewSheet) {
            ReviewSheetView(isPresented: $showReviewSheet)
        }
        .alert("Error", isPresented: Binding(
            get: { appState.errorMessage != nil },
            set: { if !$0 { appState.errorMessage = nil } }
        )) {
            Button("OK") { appState.errorMessage = nil }
        } message: {
            Text(appState.errorMessage ?? "")
        }
        .onDrop(of: [.fileURL], isTargeted: nil) { providers in
            handleDrop(providers: providers)
            return true
        }
        .toolbar {
            ToolbarItemGroup(placement: .navigation) {
                Button(action: { appState.openFolderPicker() }) {
                    Label("Open Folder", systemImage: "folder")
                }
                Button(action: { appState.openFilePicker() }) {
                    Label("Add Files", systemImage: "plus")
                }
            }
            ToolbarItemGroup(placement: .primaryAction) {
                Button(action: { appState.performUndo() }) {
                    Label("Undo", systemImage: "arrow.uturn.backward")
                }
                .disabled(!appState.canUndo)
            }
        }
    }

    private func handleDrop(providers: [NSItemProvider]) {
        for provider in providers {
            provider.loadItem(forTypeIdentifier: "public.file-url", options: nil) { item, _ in
                guard let data = item as? Data,
                      let url = URL(dataRepresentation: data, relativeTo: nil) else { return }
                DispatchQueue.main.async {
                    appState.handleDroppedURL(url)
                }
            }
        }
    }
}
