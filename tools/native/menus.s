        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        ifd COLOR
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,40(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 40(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a14,d0
        dc.w $aaa2
        tst.w (sp)+
        bne set_depth
        lsr.w #1,d5
        bne find_depth
        bra depth_ready
set_depth:
        clr.w -(sp)
        move.l 40(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        addq.l #2,sp
depth_ready:
        endif
        clr.l -(sp)
        move.w #141,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        addq.l #4,sp
        dc.w $a934
        movea.l a4,a2
        lea menu_table(pc),a3
        moveq #3,d7
create_menu:
        clr.l -(sp)
        move.w (a3)+,-(sp)
        move.w (a3)+,d0
        lea entry(pc),a0
        adda.w d0,a0
        move.l a0,-(sp)
        dc.w $a931
        move.l (sp)+,(a2)
        move.l (a2),-(sp)
        move.w (a3)+,d0
        lea entry(pc),a0
        adda.w d0,a0
        move.l a0,-(sp)
        dc.w $a933
        move.l (a2)+,-(sp)
        clr.w -(sp)
        dc.w $a935
        dbf d7,create_menu
        move.l 4(a4),-(sp)
        move.w #5,-(sp)
        dc.w $a93a
        move.l 4(a4),-(sp)
        move.w #6,-(sp)
        move.w #$100,-(sp)
        dc.w $a945
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a93a
        move.l 12(a4),-(sp)
        clr.w -(sp)
        dc.w $a93a
        ifd MENU_PALETTE
        clr.l -(sp)
        move.l #'mclr',-(sp)
        move.w #128,-(sp)
        dc.w $a9a0
        move.l (sp)+,a0
        dc.w $a029
        move.l (a0),a0
        move.w (a0)+,-(sp)
        move.l a0,-(sp)
        dc.w $aa65
        endif
        dc.w $a937
        move.w #1,44(a4)
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 16(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 16(a4),d0
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        cmp.w #20,26(a4)
        bge loop
        clr.l -(sp)
        move.l 26(a4),-(sp)
        dc.w $a93d
        bra command
key:
        btst #0,30(a4)
        beq loop
        clr.l -(sp)
        moveq #0,d0
        move.b 21(a4),d0
        move.w d0,-(sp)
        dc.w $a93e
command:
        move.l (sp)+,32(a4)
        beq unhighlight
        addq.l #1,36(a4)
unhighlight:
        clr.w -(sp)
        dc.w $a938
        bra loop
menu_table:
        dc.w 128,apple-entry,apple_items-entry
        dc.w 129,file-entry,file_items-entry
        dc.w 130,edit-entry,edit_items-entry
        dc.w 131,view-entry,view_items-entry
apple:
        dc.b 1,$14
apple_items:
        dc.b apple_end-apple_items-1,'About System 7...'
apple_end:
file:
        dc.b 4,'File'
file_items:
        dc.b file_end-file_items-1,'New/N;Open.../O;(-;Save/S;Disabled;Checked;Quit/Q'
file_end:
edit:
        dc.b 4,'Edit'
edit_items:
        dc.b edit_end-edit_items-1,'Undo/Z;(-;Cut/X;Copy/C;Paste/V;Clear'
edit_end:
view:
        dc.b 4,'View'
view_items:
        dc.b 6,'Normal'
        even
        dc.b 'S7MENUS!'
state:
        ds.b 48
