#!/usr/bin/env python3
import re

# Read the file
with open('PlantUMLEditor/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Replace the old "New Chat" button with improved header
old_button = '                <Button Content="New Chat" Grid.Column="1" Command="{Binding NewChatCommand}"></Button>'
new_header = '''                <!-- Chat Header -->
                <StackPanel Orientation="Horizontal" Padding="12,12,12,8" BorderThickness="0,0,0,1" BorderBrush="{StaticResource BorderSubtle}">
                    <TextBlock Text="Chat" FontSize="14" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}" VerticalAlignment="Center"/>
                    <Button Content="New" Grid.Column="1" Command="{Binding NewChatCommand}" Padding="8,6" Height="32" Margin="Auto,0,0,0" Background="{StaticResource PrimaryAccent}" Foreground="White" FontWeight="SemiBold" FontSize="11"/>
                </StackPanel>'''
content = content.replace(old_button, new_header)

# 2. Update ScrollViewer attributes - find and replace the opening tag
old_sv = '''                <ScrollViewer  
                  Grid.ColumnSpan="2" Grid.Row="1"
HorizontalScrollBarVisibility="Disabled"
VerticalScrollBarVisibility="Auto" x:Name="ChatScroll"
CanContentScroll="False">'''
new_sv = '''                <ScrollViewer  
                  Grid.ColumnSpan="2" Grid.Row="1"
                  HorizontalScrollBarVisibility="Disabled"
                  VerticalScrollBarVisibility="Auto" x:Name="ChatScroll"
                  CanContentScroll="False"
                  Background="{StaticResource BackgroundMain}"
                  Padding="8">'''
content = content.replace(old_sv, new_sv)

# 3. Replace the Border styling with new palette colors
old_border = '''                                <Border BorderThickness="1" CornerRadius="5"  BorderBrush="Gray" Margin="5"   >
                                    <Border.Style>
                                        <Style TargetType="Border">
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding IsUser}" Value="True">
                                                    <DataTrigger.Setters>
                                                        <Setter   Property="Background" Value="LightBlue">
                                                        </Setter>
                                                        <Setter   Property="HorizontalAlignment" Value="Right"/>
                                                    </DataTrigger.Setters>
                                                </DataTrigger>
                                                <DataTrigger Binding="{Binding IsUser}" Value="False">
                                                    <DataTrigger.Setters>
                                                        <Setter   Property="Background" Value="LightGray">
                                                        </Setter>
                                                    </DataTrigger.Setters>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Border.Style>
                                    <StackPanel Orientation="Vertical">'''

new_border = '''                                <Border CornerRadius="8" Margin="0,6,0,6">
                                    <Border.Style>
                                        <Style TargetType="Border">
                                            <Setter Property="BorderThickness" Value="1"/>
                                            <Setter Property="BorderBrush" Value="{StaticResource BorderSubtle}"/>
                                            <Setter Property="Background" Value="{StaticResource BackgroundSecondary}"/>
                                            <Setter Property="Padding" Value="12"/>
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding IsUser}" Value="True">
                                                    <DataTrigger.Setters>
                                                        <Setter Property="Background" Value="{StaticResource BackgroundSelected}"/>
                                                        <Setter Property="BorderBrush" Value="{StaticResource PrimaryAccent}"/>
                                                        <Setter   Property="HorizontalAlignment" Value="Right"/>
                                                        <Setter Property="MaxWidth" Value="85%"/>
                                                        <Setter Property="Margin" Value="20,6,0,6"/>
                                                    </DataTrigger.Setters>
                                                </DataTrigger>
                                                <DataTrigger Binding="{Binding IsUser}" Value="False">
                                                    <DataTrigger.Setters>
                                                        <Setter Property="Background" Value="{StaticResource BackgroundMain}"/>
                                                        <Setter Property="Margin" Value="0,6,20,6"/>
                                                    </DataTrigger.Setters>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Border.Style>
                                    <StackPanel Orientation="Vertical" Margin="0">'''

content = content.replace(old_border, new_border)

# 4. Update Tool Calls ItemTemplate styling
old_tool_calls = '''                                            <ListBox ItemsSource="{Binding ToolCalls}">
                                                <ListBox.ItemTemplate>
                                                    <DataTemplate>
                                                        <StackPanel Orientation="Vertical">
                                                            <TextBlock Text="{Binding ToolName}" FontWeight="Bold"></TextBlock>
                                                            <TextBlock Text="{Binding Arguments}"></TextBlock>'''

new_tool_calls = '''                                            <ListBox ItemsSource="{Binding ToolCalls}">
                                                <ListBox.ItemTemplate>
                                                    <DataTemplate>
                                                        <StackPanel Orientation="Vertical" Margin="0,4,0,4">
                                                            <TextBlock Text="{Binding ToolName}" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/>
                                                            <TextBlock Text="{Binding Arguments}" FontSize="11" Foreground="{StaticResource TextSecondary}" TextWrapping="Wrap" Margin="0,4,0,0"/>'''

content = content.replace(old_tool_calls, new_tool_calls)

# 5. Update Undo Grid row definitions
old_undo_grid = '''                                            <Grid >
                                                <Grid.RowDefinitions>
                                                    <RowDefinition></RowDefinition>
                                                    <RowDefinition></RowDefinition>
                                                </Grid.RowDefinitions>'''

new_undo_grid = '''                                            <Grid Margin="0,8,0,0">
                                                <Grid.RowDefinitions>
                                                    <RowDefinition Height="*"/>
                                                    <RowDefinition Height="Auto"/>
                                                </Grid.RowDefinitions>'''

content = content.replace(old_undo_grid, new_undo_grid)

# 6. Update Undo ListBox ItemTemplate
old_undo_template = '''                                                <ListBox ItemsSource="{Binding Undos}">
                                                    <ListBox.ItemTemplate>
                                                        <DataTemplate>
                                                            <StackPanel Orientation="Vertical">
                                                                <TextBlock Text="{Binding undoType}" FontWeight="Bold"></TextBlock>
                                                                <TextBlock Text="{Binding fileName}"></TextBlock>
                                                                <TextBlock Text="{Binding textBefore}"></TextBlock>'''

new_undo_template = '''                                                <ListBox ItemsSource="{Binding Undos}">
                                                    <ListBox.ItemTemplate>
                                                        <DataTemplate>
                                                            <StackPanel Orientation="Vertical" Margin="0,4,0,4">
                                                                <TextBlock Text="{Binding undoType}" FontWeight="SemiBold" Foreground="{StaticResource TextPrimary}"/>
                                                                <TextBlock Text="{Binding fileName}" FontSize="11" Foreground="{StaticResource TextSecondary}"/>
                                                                <TextBlock Text="{Binding textBefore}" FontSize="10" Foreground="{StaticResource TextLight}" TextWrapping="Wrap" Margin="0,4,0,0"/>'''

content = content.replace(old_undo_template, new_undo_template)

# 7. Update Undo button styling
old_undo_btn = '''                                                <Button Grid.Row="1"  CommandParameter="{Binding Undos}" Command="{Binding DataContext.UndoAIEditsCommand, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Window}, AncestorLevel=1}}">Undo Operations</Button>'''

new_undo_btn = '''                                                <Button Grid.Row="1"  CommandParameter="{Binding Undos}" Command="{Binding DataContext.UndoAIEditsCommand, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Window}, AncestorLevel=1}}" Padding="8,6" Margin="0,8,0,0" Background="{StaticResource WarningColor}" Foreground="White" FontWeight="SemiBold" FontSize="11">Undo Operations</Button>'''

content = content.replace(old_undo_btn, new_undo_btn)

# 8. Update Running TextBlock
old_running = '''                                        <TextBlock Foreground="Green">
                                            <TextBlock.Style>
                                                <Style TargetType="TextBlock">
                                                    <Setter Property="Visibility" Value="Visible" />
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding IsBusy}" Value="False">
                                                            <Setter Property="Visibility" Value="Hidden" />
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </TextBlock.Style>
                                          Running
                                        </TextBlock>'''

new_running = '''                                        <TextBlock Margin="0,8,0,0" Foreground="{StaticResource SuccessColor}" FontSize="11" FontWeight="SemiBold">
                                            <TextBlock.Style>
                                                <Style TargetType="TextBlock">
                                                    <Setter Property="Visibility" Value="Visible" />
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding IsBusy}" Value="False">
                                                            <Setter Property="Visibility" Value="Hidden" />
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </TextBlock.Style>
                                            ⟳ Running
                                        </TextBlock>'''

content = content.replace(old_running, new_running)

# 9. Update FlowDocumentScrollViewer
old_flow = '''                                        <FlowDocumentScrollViewer ScrollViewer.VerticalScrollBarVisibility="Disabled" Padding="5" FontFamily="Cascadia Code" Document="{Binding Document}"  >'''

new_flow = '''                                        <FlowDocumentScrollViewer ScrollViewer.VerticalScrollBarVisibility="Disabled" Padding="0" FontFamily="Segoe UI" FontSize="12" Foreground="{StaticResource TextPrimary}" Document="{Binding Document}" Margin="0,8,0,0">'''

content = content.replace(old_flow, new_flow)

# 10. Replace the entire input area
old_input = '''                </ScrollViewer>
                <TextBox Grid.Row="2"  FontFamily="Cascadia Code"  AcceptsReturn="False" AcceptsTab="True" Text="{Binding ChatText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}">
                    <b:Interaction.Triggers>
                        <b:KeyTrigger Key="Return" ActiveOnFocus="True">
                            <b:InvokeCommandAction Command="{Binding SendChatCommand}"  CommandParameter="{Binding CurrentDocument}" />
                        </b:KeyTrigger>
                    </b:Interaction.Triggers>

                </TextBox>
                <Button Grid.Row="2" Grid.Column="1" Content="Send" Command="{Binding SendChatCommand}"  CommandParameter="{Binding CurrentDocument}"></Button>'''

new_input = '''                </ScrollViewer>
                
                <!-- Input Area -->
                <Border Grid.Row="2" Grid.ColumnSpan="2" BorderThickness="0,1,0,0" BorderBrush="{StaticResource BorderSubtle}" Background="{StaticResource BackgroundSecondary}" Padding="8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox 
                            FontFamily="Segoe UI" 
                            FontSize="12"
                            AcceptsReturn="False" 
                            AcceptsTab="True" 
                            Text="{Binding ChatText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                            Padding="10,10"
                            MinHeight="44"
                            MaxHeight="100"
                            VerticalScrollBarVisibility="Auto"
                            BorderThickness="1"
                            BorderBrush="{StaticResource BorderSubtle}"
                            Background="{StaticResource BackgroundMain}">
                            <b:Interaction.Triggers>
                                <b:KeyTrigger Key="Return" ActiveOnFocus="True">
                                    <b:InvokeCommandAction Command="{Binding SendChatCommand}" CommandParameter="{Binding CurrentDocument}" />
                                </b:KeyTrigger>
                            </b:Interaction.Triggers>
                        </TextBox>
                        <Button 
                            Grid.Column="1"
                            Content="Send" 
                            Command="{Binding SendChatCommand}" 
                            CommandParameter="{Binding CurrentDocument}"
                            Padding="12,10"
                            MinWidth="80"
                            Height="44"
                            Margin="8,0,0,0"
                            Background="{StaticResource PrimaryAccent}"
                            Foreground="White"
                            FontWeight="SemiBold"
                            FontSize="12"/>
                    </Grid>
                </Border>'''

content = content.replace(old_input, new_input)

# Write back
with open('PlantUMLEditor/MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(content)

print("✅ Chat UI modernization complete!")
