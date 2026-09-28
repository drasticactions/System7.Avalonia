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
        move.l (sp)+,100(a4)
        moveq #32,d5
find_depth:
        clr.w -(sp)
        move.l 100(a4),-(sp)
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
        move.w d5,106(a4)
        clr.w -(sp)
        move.l 100(a4),-(sp)
        move.w d5,-(sp)
        move.l #$00010001,-(sp)
        move.w #$0a13,d0
        dc.w $aaa2
        move.w (sp)+,104(a4)
depth_ready:
        endif
        clr.l -(sp)
        move.w #128,-(sp)
        clr.l -(sp)
        move.l #-1,-(sp)
        ifd COLOR
        dc.w $aa46
        else
        dc.w $a9bd
        endif
        move.l (sp)+,(a4)
        move.l (a4),-(sp)
        dc.w $a873
        lea 4(a4),a3
        move.w #128,d3
create:
        clr.l -(sp)
        move.w d3,-(sp)
        move.l (a4),-(sp)
        dc.w $a9be
        move.l (sp)+,(a3)+
        addq.w #1,d3
        cmp.w #142,d3
        blt create
loop:
        clr.w -(sp)
        move.w #$ffff,-(sp)
        pea 60(a4)
        dc.w $a970
        tst.b (sp)+
        beq loop
        move.w 60(a4),d0
        cmp.w #6,d0
        beq update
        cmp.w #3,d0
        beq key
        cmp.w #1,d0
        bne loop
        clr.w -(sp)
        move.l 70(a4),-(sp)
        pea 76(a4)
        dc.w $a92c
        cmp.w #3,(sp)+
        bne loop
        move.l (a4),-(sp)
        dc.w $a91f
        move.l (a4),-(sp)
        dc.w $a873
        move.l 70(a4),80(a4)
        pea 80(a4)
        dc.w $a871
        clr.w -(sp)
        move.l 80(a4),-(sp)
        move.l (a4),-(sp)
        pea 84(a4)
        dc.w $a96c
        move.w (sp)+,88(a4)
        beq loop
        clr.w -(sp)
        move.l 84(a4),-(sp)
        move.l 80(a4),-(sp)
        clr.l -(sp)
        dc.w $a968
        move.w (sp)+,90(a4)
        addq.l #1,92(a4)
        bra loop
key:
        move.b 65(a4),d0
        cmp.b #'d',d0
        beq disabled
        cmp.b #'i',d0
        beq inactive
        cmp.b #'a',d0
        beq active
        cmp.b #'h',d0
        beq hide
        cmp.b #'s',d0
        beq show
        cmp.b #'r',d0
        beq reset
        cmp.b #'g',d0
        beq gray_background
        cmp.b #'t',d0
        beq white_background
        cmp.b #'p',d0
        beq shifted
        cmp.b #'e',d0
        beq unshifted
        cmp.b #'n',d0
        beq narrow
        cmp.b #'w',d0
        beq wide
        cmp.b #'b',d0
        beq redraw
        sub.b #'0',d0
        bmi loop
        cmp.b #9,d0
        bgt loop
        and.w #$ff,d0
        add.w d0,d0
        lea values(pc),a0
        move.w (a0,d0.w),d4
        lea 32(a4),a3
        moveq #5,d3
set_value:
        move.l (a3)+,-(sp)
        move.w d4,-(sp)
        dc.w $a963
        dbra d3,set_value
        bra loop
reset:
        lea initial(pc),a2
        lea 32(a4),a3
        moveq #5,d3
reset_value:
        move.l (a3)+,-(sp)
        move.w (a2)+,-(sp)
        dc.w $a963
        dbra d3,reset_value
        bra loop
disabled:
        move.w #255,d4
        bra hilite
inactive:
        move.w #254,d4
        bra hilite
active:
        moveq #0,d4
hilite:
        lea 4(a4),a3
        moveq #13,d3
set_hilite:
        move.l (a3)+,-(sp)
        move.w d4,-(sp)
        dc.w $a95d
        dbra d3,set_hilite
        bra loop
hide:
        move.l 4(a4),-(sp)
        dc.w $a958
        move.l 32(a4),-(sp)
        dc.w $a958
        bra loop
show:
        move.l 4(a4),-(sp)
        dc.w $a957
        move.l 32(a4),-(sp)
        dc.w $a957
        bra loop
shifted:
        moveq #21,d0
        bra move_control
unshifted:
        moveq #20,d0
move_control:
        move.l 4(a4),-(sp)
        move.w d0,-(sp)
        move.w #20,-(sp)
        dc.w $a959
        bra loop
narrow:
        moveq #16,d0
        bra resize
wide:
        moveq #32,d0
resize:
        move.l 32(a4),-(sp)
        move.w d0,-(sp)
        move.w d0,-(sp)
        dc.w $a95c
        bra loop
gray_background:
        lea gray_pattern(pc),a2
        bra background
white_background:
        lea white_pattern(pc),a2
background:
        move.l (a4),-(sp)
        dc.w $a873
        pea client_rect(pc)
        move.l a2,-(sp)
        dc.w $a8a5
redraw:
        move.l (a4),-(sp)
        dc.w $a969
        addq.l #1,96(a4)
        bra loop
update:
        move.l 62(a4),-(sp)
        dc.w $a922
        move.l (a4),-(sp)
        dc.w $a873
        move.l (a4),-(sp)
        dc.w $a969
        move.l 62(a4),-(sp)
        dc.w $a923
        addq.l #1,96(a4)
        bra loop
client_rect:
        dc.w 0,0,260,430
gray_pattern:
        dc.b $aa,$55,$aa,$55,$aa,$55,$aa,$55
white_pattern:
        ds.b 8
initial:
        dc.w 0,16,17,50,99,100
values:
        dc.w 0,16,17,33,34,50,66,67,99,100
        dc.b 'S7MEDIA!'
state:
        ds.b 112
