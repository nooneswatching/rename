import SwiftUI

struct PreviewBarView: View {
    @EnvironmentObject var appState: AppState
    var onApply: () -> Void
    var body: some View {
        HStack {
            Text("Preview Bar")
            Spacer()
            Button("Apply Rename", action: onApply)
                .disabled(appState.files.isEmpty)
        }
        .padding(.horizontal)
    }
}
