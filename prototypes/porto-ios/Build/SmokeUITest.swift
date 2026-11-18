import XCTest

final class SmokeUITest: XCTestCase {
    func testPhotoLibraryTransfer() {
        let app = XCUIApplication(bundleIdentifier: "it.porto.test.photosmoke")
        app.launch()
        let system = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let outcome = app.staticTexts["result"]
        let until = Date().addingTimeInterval(120)
        while Date() < until {
            if outcome.exists && outcome.label != "In corso" {
                XCTAssertEqual(outcome.label, "OK")
                return
            }
            // Only the throwaway simulator can run this test. Confirm its normal system prompts.
            for title in ["Allow Full Access", "Allow Access to All Photos", "Allow", "OK"] {
                let choice = system.alerts.buttons[title].firstMatch
                if choice.exists && choice.isHittable { choice.tap(); break }
            }
            Thread.sleep(forTimeInterval: 1)
        }
        XCTFail("Esito atteso dal simulatore. Interfaccia: " + system.alerts.debugDescription + app.debugDescription)
    }
}
