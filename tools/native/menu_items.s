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
        move.l (sp)+,56(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 56(a4),-(sp)
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
        move.l 56(a4),-(sp)
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
        move.w #128,d7
create:
        clr.l -(sp)
        move.w d7,-(sp)
        dc.w $a9bf
        move.l (sp)+,(a2)
        move.l (a2)+,-(sp)
        moveq #0,d0
        cmp.w #134,d7
        blt insert
        moveq #-1,d0
insert:
        move.w d0,-(sp)
        dc.w $a935
        addq.w #1,d7
        cmp.w #136,d7
        bne create
        lea 72(a4),a2
        moveq #0,d7
measure_styles:
        move.w d7,-(sp)
        dc.w $a888
        move.l a2,-(sp)
        dc.w $a88b
        clr.w -(sp)
        pea measure_text(pc)
        dc.w $a88c
        move.w (sp)+,8(a2)
        adda.w #10,a2
        addq.w #1,d7
        cmp.w #128,d7
        bne measure_styles
        clr.w -(sp)
        dc.w $a888
        lea 1354(a4),a2
        moveq #-32,d6
angle_row:
        moveq #-32,d7
angle_column:
        subq.l #6,sp
        move.w d7,-(sp)
        move.w d6,-(sp)
        dc.w $a869
        dc.w $a8c4
        move.w (sp)+,(a2)+
        addq.w #1,d7
        cmp.w #33,d7
        bne angle_column
        addq.w #1,d6
        cmp.w #33,d6
        bne angle_row
        dc.w $a937
        move.w #8,62(a4)
        move.w #1,60(a4)
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 32(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 32(a4),d0
        cmp.w #6,d0
        beq update
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        cmp.w #20,42(a4)
        bge loop
        clr.l -(sp)
        move.l 42(a4),-(sp)
        dc.w $a93d
        bra command
update:
        move.l 34(a4),-(sp)
        dc.w $a922
        move.l 34(a4),-(sp)
        dc.w $a923
        bra loop
key:
        btst #0,46(a4)
        beq style_key
        clr.l -(sp)
        moveq #0,d0
        move.b 37(a4),d0
        move.w d0,-(sp)
        dc.w $a93e
        bra command
style_key:
        moveq #0,d0
        move.b 37(a4),d0
        cmp.w #'x',d0
        beq toggle_enabled
        cmp.w #'r',d0
        beq relocate_menu
        cmp.w #'i',d0
        beq toggle_icons
        cmp.w #'a',d0
        blt digit_key
        sub.w #'a'-'0'-10,d0
digit_key:
        sub.w #'0',d0
        cmp.w #15,d0
        bhi loop
        move.w d0,64(a4)
        lsl.w #3,d0
        move.w d0,d6
        moveq #1,d7
change_style:
        move.l 4(a4),-(sp)
        move.w d7,-(sp)
        move.w d6,-(sp)
        dc.w $a942
        addq.w #1,d6
        addq.w #1,d7
        cmp.w #9,d7
        bne change_style
        move.l 4(a4),-(sp)
        dc.w $a948
        addq.l #1,68(a4)
        bra loop
relocate_menu:
        eori.w #1,1352(a4)
        move.w #130,-(sp)
        dc.w $a936
        move.l 8(a4),-(sp)
        moveq #0,d0
        tst.w 1352(a4)
        bne insert_relocated
        move.w #131,d0
insert_relocated:
        move.w d0,-(sp)
        dc.w $a935
        dc.w $a937
        addq.l #1,68(a4)
        bra loop
toggle_enabled:
        eori.w #1,66(a4)
        moveq #0,d5
toggle_menu:
        move.w #9,d6
        tst.w d5
        beq toggle_start
        move.w #8,d6
toggle_start:
        moveq #1,d7
toggle_item:
        move.l (a4,d5.w),-(sp)
        move.w d7,-(sp)
        tst.w 66(a4)
        beq enable_item
        dc.w $a93a
        bra toggled_item
enable_item:
        dc.w $a939
toggled_item:
        addq.w #1,d7
        cmp.w d6,d7
        ble toggle_item
        addq.w #4,d5
        cmp.w #8,d5
        bne toggle_menu
        addq.l #1,68(a4)
        bra loop
toggle_icons:
        eori.w #1,9804(a4)
        moveq #1,d7
toggle_icon:
        move.l 20(a4),-(sp)
        move.w d7,-(sp)
        tst.w 9804(a4)
        beq enable_icon
        dc.w $a93a
        bra icon_toggled
enable_icon:
        dc.w $a939
icon_toggled:
        addq.w #1,d7
        cmp.w #11,d7
        bne toggle_icon
        addq.l #1,68(a4)
        bra loop
command:
        move.l (sp)+,48(a4)
        beq unhighlight
        addq.l #1,52(a4)
unhighlight:
        clr.w -(sp)
        dc.w $a938
        bra loop
measure_text:
        dc.b 9,'Agj pq _/'
        even
        dc.b 'S7MITEMS'
state:
        ds.b 9806
