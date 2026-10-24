#model SlusserLabs.AnglePlates.Samples.HelloWorld.GreetingModel
#generate Text
# Hello, [[ model.Name ]]!

Project: [[ model.ProjectName ]]

[% if (model.IsFirstVisit) { -%]
Welcome! Let's build your first template.

[% } else { -%]
Welcome back! Here's what's next.

[% } -%]

[% if (model.Items.Length > 0) { -%]
## Next steps ([[ model.Items.Length ]])

[% for (int i = 0; i < model.Items.Length; i++) { -%]
[[ i + 1 ]]. [[ model.Items[i] ]]
[% } -%]
[% } else { -%]
You're all caught up!
[% } -%]
