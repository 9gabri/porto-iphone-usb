require 'xcodeproj'

dir = File.expand_path(ARGV.fetch(0))
project = Xcodeproj::Project.new(File.join(dir, 'Smoke.xcodeproj'))
app = project.new_target(:application, 'Smoke', :ios, '17.0')
tests = project.new_target(:ui_test_bundle, 'SmokeUITests', :ios, '17.0')
tests.add_dependency(app)
files = project.main_group.new_group('Sources')
%w[Transfer.swift PhotoKitSmoke.swift].each do |name|
  app.add_file_references([files.new_file(File.join(dir, name))])
end
tests.add_file_references([files.new_file(File.join(dir, 'SmokeUITest.swift'))])
[[app, 'it.porto.test.photosmoke'], [tests, 'it.porto.test.photosmoke.ui']].each do |target, id|
  target.build_configurations.each do |cfg|
    cfg.build_settings.merge!('PRODUCT_BUNDLE_IDENTIFIER'=>id, 'SWIFT_VERSION'=>'5.0',
      'TARGETED_DEVICE_FAMILY'=>'1', 'CODE_SIGNING_ALLOWED'=>'NO', 'PRODUCT_NAME'=>'$(TARGET_NAME)')
    if target == app
      cfg.build_settings.merge!('INFOPLIST_FILE'=>'Info.plist', 'SWIFT_ACTIVE_COMPILATION_CONDITIONS'=>'PORTO_SMOKE')
    else
      cfg.build_settings.merge!('TEST_TARGET_NAME'=>'Smoke', 'GENERATE_INFOPLIST_FILE'=>'YES')
    end
  end
end
scheme = Xcodeproj::XCScheme.new
scheme.add_build_target(app)
scheme.add_test_target(tests)
scheme.set_launch_target(app)
project.save
scheme.save_as(project.path, 'Smoke', true)
