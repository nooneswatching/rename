import SwiftUI

struct ReviewSheetView: View {
    @EnvironmentObject var appState: AppState
    @Binding var isPresented: Bool
    var body: some View {
        VStack {
            Text("Review Sheet")
            Button("Cancel") { isPresented = false }
        }
        .frame(width: 600, height: 500)
    }
}
