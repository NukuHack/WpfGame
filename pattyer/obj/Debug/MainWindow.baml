   M S B A M L   `   `   `  ����  pattyer    pattyer.MainWindow   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	     � Title$  
Space Game��5   	   $	��450��6   $	��800��6)    � WindowState$ 	Maximized=�5   	    J� SnapsToDevicePixels .  � UseLayoutRounding .  � WindowStartupLocation$ CenterScreen=�5
   "    � 	Resources 5         ButtonStyle �� 
TargetType �� Property �� Value	 �� 
TargetType
 �� CornerRadius �� Padding��%�  &	        �� 5   
   " ��6$   .�5      ��  � �� �	#FFEA39396+   �� 5       � ��
 �   �6,   �� 5       � ��$ 20(�6)   �� 5       � ��$
 Bold"�6+   �� 5       �@�Cursor$
 Hand��6'   �� 5       � �� 5      �� 5      "	 ��6&   .R�5      �� #��z��6!   $
 20��6K   $ 5q�6]   ._�5      �� $��Center=�6/   $��Center=�6L   5      5      5      5      5      .��5       � -   5         �Name$ GameGrid��5       .+�5"   
   �� -   5         ��Name$ BackgroundCanvas��5"      �� 5%   
   -   5        $ 	MapCanvas��5%      
������ 6$   �� 5(   
   -   5        $ 
GameCanvas��5(      
������ 6%   �� 5+   
   -   5         ��Name$ CloseButton��5+      $��Close��5,      #��[  5-      $��1,0,0,1q�5.      $
��Left=�6"   $	��100��6=   $��45��6I   $��Bottom=�6U   5/      50      