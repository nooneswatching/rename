import SwiftUI

struct SidebarView: View {
    @EnvironmentObject var appState: AppState
    var body: some View {
        Text("Sidebar")
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
