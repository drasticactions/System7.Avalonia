        org 0
        dc.w 0,1
entry:
        pea -4(a5)
        dc.w $a86e
        dc.w $a8fe
        dc.w $a912
        dc.w $a930
        dc.w $a9cc
        clr.l -(sp)
        dc.w $a97b
        lea state(pc),a4
        clr.l -(sp)
        dc.w $aa2a
        move.l (sp)+,88(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 88(a4),-(sp)
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
        move.w d5,94(a4)
        clr.w -(sp)
        move.l 88(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        move.w (sp)+,92(a4)
depth_ready:
        dc.w $a850
        dc.w $a852
        lea state(pc),a4
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        dc.w $a97c
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        moveq #5,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #6,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #8,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        moveq #2,d3
        bsr disable
        moveq #6,d3
        bsr disable
        moveq #9,d3
        bsr disable
        moveq #13,d3
        bsr disable
        moveq #1,d3
        lea 20(a4),a3
save_handles:
        bsr item
        move.l 8(a4),(a3)+
        addq.w #1,d3
        cmp.w #14,d3
        ble save_handles
loop:
        pea filter(pc)
        pea 4(a4)
        dc.w $a991
        move.w 4(a4),d3
        cmp.w #3,d3
        beq reset_scroll
        cmp.w #4,d3
        blt loop
        cmp.w #9,d3
        bgt loop
        cmp.w #7,d3
        bge radio
        bsr item
        clr.w -(sp)
        move.l 8(a4),-(sp)
        dc.w $a960
        move.w (sp)+,d0
        eor.w #1,d0
        move.l 8(a4),-(sp)
        move.w d0,-(sp)
        dc.w $a963
        bra loop
reset_scroll:
        moveq #11,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #50,-(sp)
        dc.w $a963
        moveq #12,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #50,-(sp)
        dc.w $a963
        bra loop
radio:
        move.w d3,d4
        moveq #7,d3
        bsr item
        move.l 8(a4),-(sp)
        clr.w -(sp)
        dc.w $a963
        moveq #8,d3
        bsr item
        move.l 8(a4),-(sp)
        clr.w -(sp)
        dc.w $a963
        move.w d4,d3
        bsr item
        move.l 8(a4),-(sp)
        move.w #1,-(sp)
        dc.w $a963
        bra loop
item:
        move.l (a4),-(sp)
        move.w d3,-(sp)
        pea 6(a4)
        pea 8(a4)
        pea 12(a4)
        dc.w $a98d
        rts
disable:
        bsr item
        move.l 8(a4),-(sp)
        move.w #255,-(sp)
        dc.w $a95d
        rts
filter:
        link a6,#0
        movem.l d3-d7/a2-a4,-(sp)
        lea state(pc),a4
        move.l 12(a6),a3
        cmp.w #3,(a3)
        bne filter_done
        move.b 5(a3),d0
        cmp.b #'c',d0
        beq custom_color
        cmp.b #'z',d0
        beq default_color
        cmp.b #'r',d0
        beq redraw
        cmp.b #'d',d0
        beq disable_all
        cmp.b #'e',d0
        beq enable_all
        cmp.b #'p',d0
        beq shift_controls
        cmp.b #'i',d0
        beq inactive_scrollbars
        bra filter_done
inactive_scrollbars:
        move.w #254,d4
        moveq #11,d3
        bra hilite_loop
shift_controls:
        moveq #1,d4
        tst.b 86(a4)
        beq shift_ready
        neg.w d4
shift_ready:
        not.b 86(a4)
        moveq #1,d3
shift_loop:
        cmp.w #10,d3
        beq next_shift
        bsr item
        move.l 8(a4),a0
        move.l (a0),a1
        move.w 10(a1),d0
        add.w d4,d0
        move.l a0,-(sp)
        move.w d0,-(sp)
        move.w 8(a1),-(sp)
        dc.w $a959
next_shift:
        addq.w #1,d3
        cmp.w #14,d3
        ble shift_loop
        bra redraw
custom_color:
        move.w #128,d4
        bra set_colors
default_color:
        move.w #129,d4
set_colors:
        move.w d4,80(a4)
        clr.l -(sp)
        move.l #'cctb',-(sp)
        move.w d4,-(sp)
        dc.w $a9a0
        move.l (sp)+,76(a4)
        moveq #1,d3
color_loop:
        cmp.w #10,d3
        beq next_color
        bsr item
        move.l 8(a4),-(sp)
        move.l 76(a4),-(sp)
        dc.w $aa43
next_color:
        addq.w #1,d3
        cmp.w #14,d3
        ble color_loop
        bra redraw
disable_all:
        move.w #255,d4
        bra set_hilite
enable_all:
        moveq #0,d4
set_hilite:
        moveq #1,d3
hilite_loop:
        cmp.w #10,d3
        beq next_hilite
        bsr item
        move.l 8(a4),-(sp)
        move.w d4,-(sp)
        dc.w $a95d
next_hilite:
        addq.w #1,d3
        cmp.w #14,d3
        ble hilite_loop
redraw:
        move.l (a4),-(sp)
        dc.w $a873
        move.l (a4),a0
        pea 16(a0)
        dc.w $a8a3
        move.l (a4),-(sp)
        dc.w $a981
        addq.l #1,82(a4)
        clr.w (a3)
filter_done:
        clr.b 20(a6)
        movem.l (sp)+,d3-d7/a2-a4
        unlk a6
        move.l (sp)+,a0
        lea 12(sp),sp
        jmp (a0)
        dc.b 'S7COLOR!'
state:
        ds.b 96
