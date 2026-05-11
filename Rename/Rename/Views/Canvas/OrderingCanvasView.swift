import SwiftUI

struct OrderingCanvasView: View {
    @EnvironmentObject var appState: AppState
    var body: some View {
        Text("Ordering Canvas")
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
