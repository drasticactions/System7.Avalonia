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
        moveq #1,d3
        lea 36(a4),a3
save_handles:
        bsr item
        move.l 8(a4),(a3)+
        addq.w #1,d3
        cmp.w #4,d3
        ble save_handles
        clr.w -(sp)
        pea 20(a4)
        move.w #$0203,d0
        dc.w $aa68
        move.w (sp)+,28(a4)
        move.w #1,24(a4)
        bsr set_default
        clr.w -(sp)
        move.l (a4),-(sp)
        move.w #2,-(sp)
        move.w #$0305,d0
        dc.w $aa68
        move.w (sp)+,26(a4)
loop:
        pea filter(pc)
        pea 4(a4)
        dc.w $a991
        addq.l #1,30(a4)
        move.w 4(a4),34(a4)
        move.l $016a,52(a4)
        bra loop
filter:
        link a6,#0
        movem.l d3-d7/a2-a4,-(sp)
        lea state(pc),a4
        move.l 12(a6),a3
        cmp.w #3,(a3)
        bne standard
        move.l (a3),56(a4)
        move.l 4(a3),60(a4)
        move.l 8(a3),64(a4)
        move.l 12(a3),68(a4)
        move.b 5(a3),d0
        cmp.b #'d',d0
        beq disable
        cmp.b #'e',d0
        beq enable
        cmp.b #'r',d0
        beq redraw
        cmp.b #'h',d0
        beq hide
        cmp.b #'s',d0
        beq show
        cmp.b #'1',d0
        blt standard
        cmp.b #'4',d0
        bgt standard
        sub.b #'0',d0
        and.w #$ff,d0
        move.w d0,24(a4)
        bsr set_default
        bra redraw
disable:
        move.w #255,d4
        bra hilite
enable:
        moveq #0,d4
hilite:
        move.w 24(a4),d3
        bsr item
        move.l 8(a4),-(sp)
        move.w d4,-(sp)
        dc.w $a95d
        bra consumed
redraw:
        move.l (a4),-(sp)
        dc.w $a873
        move.l (a4),a0
        pea 16(a0)
        dc.w $a8a3
        move.l (a4),-(sp)
        dc.w $a981
        move.w #6,(a3)
        move.l (a4),2(a3)
        bra standard
hide:
        move.l (a4),-(sp)
        move.w 24(a4),-(sp)
        dc.w $a827
        bra consumed
show:
        move.l (a4),-(sp)
        move.w 24(a4),-(sp)
        dc.w $a828
        bra consumed
consumed:
        clr.w (a3)
standard:
        clr.w -(sp)
        move.l 16(a6),-(sp)
        move.l 12(a6),-(sp)
        move.l 8(a6),-(sp)
        move.l 20(a4),a0
        jsr (a0)
        move.w (sp)+,20(a6)
        movem.l (sp)+,d3-d7/a2-a4
        unlk a6
        move.l (sp)+,a0
        lea 12(sp),sp
        jmp (a0)
set_default:
        clr.w -(sp)
        move.l (a4),-(sp)
        move.w 24(a4),-(sp)
        move.w #$0304,d0
        dc.w $aa68
        move.w (sp)+,28(a4)
        rts
item:
        move.l (a4),-(sp)
        move.w d3,-(sp)
        pea 6(a4)
        pea 8(a4)
        pea 12(a4)
        dc.w $a98d
        rts
        dc.b 'S7DIALOG'
state:
        ds.b 72
