import SwiftUI

struct RulesPanelView: View {
    @EnvironmentObject var appState: AppState
    var body: some View {
        Text("Rules Panel")
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
